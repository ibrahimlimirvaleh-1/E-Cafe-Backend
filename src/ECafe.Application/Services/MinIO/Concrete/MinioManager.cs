using AutoMapper;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.File;
using ECafe.Application.Services;
using ECafe.Application.Services.FileValidation;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace ECafe.Infrastructure.Services.MinIO
{
    public class MinioManager : BaseManager, IMinioService
    {
        private readonly IMinioClient _minioClient;
        private readonly string _bucket;
        private readonly string? _publicBaseUrl;
        private readonly ILogger<MinioManager> _logger;
        private bool _bucketExists;
        private readonly SemaphoreSlim _bucketLock = new(1, 1);

        public MinioManager(IHttpContextAccessor httpContextAccessor,
                            IMapper mapper,
                            IConfiguration configuration,
                            ILogger<MinioManager> logger)
                            : base(httpContextAccessor, mapper, configuration)
        {
            _logger = logger;
            _bucket = configuration["Minio:BucketName"]
                ?? throw new InvalidOperationException("Minio:BucketName is not configured.");
            _publicBaseUrl = configuration["FileStorage:PublicBaseUrl"]?.TrimEnd('/');
            var endpoint = configuration["Minio:Endpoint"]
                ?? throw new InvalidOperationException("Minio:Endpoint is not configured.");
            var accessKey = configuration["Minio:AccessKey"]
                ?? throw new InvalidOperationException("Minio:AccessKey is not configured.");
            var secretKey = configuration["Minio:SecretKey"]
                ?? throw new InvalidOperationException("Minio:SecretKey is not configured.");

            var useSsl = bool.TryParse(configuration["MinIO:UseSSL"], out var parsedUseSsl) && parsedUseSsl;

            _minioClient = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(useSsl)
                .Build();
        }



        public async Task<GetFileResponse> GetFileAsync(string token)
        {
            ValidateFileToken(token);

            return await ExecuteMinioAsync("download file", async () =>
            {
                await EnsureBucketExistsAsync();

                var contentType = await GetFileContentTypeAsync(token);
                var destination = new MemoryStream();
                var getObjectArgs = new GetObjectArgs()
                    .WithBucket(_bucket)
                    .WithObject(token)
                    .WithCallbackStream(stream => stream.CopyTo(destination));

                await _minioClient.GetObjectAsync(getObjectArgs);

                return new GetFileResponse(destination.ToArray(), contentType);
            }, token);
        }

        public async Task<string> UploadFileAsync(UploadFileDto request)
            => await UploadFileAsync(request, DefaultUploadPolicy);

        public async Task<string> UploadFileAsync(UploadFileDto request, FileUploadPolicy policy)
        {
            if (request.File is null || request.File.Length == 0)
                throw new ArgumentException("File is required.", nameof(request));

            FileUploadValidation.EnsureSizeIsAllowed(request.File.Length, policy);

            var contentType = FileUploadValidation.ValidateFileNameAndContentType(
                request.File.FileName,
                request.File.ContentType,
                policy);
            await ValidateFileSignatureAsync(request.File, contentType);

            return await ExecuteMinioAsync("upload file", async () =>
            {
                await EnsureBucketExistsAsync();

                var filename = Guid.NewGuid().ToString();
                await using var filestream = request.File.OpenReadStream();

                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(_bucket)
                    .WithObject(filename)
                    .WithStreamData(filestream)
                    .WithObjectSize(filestream.Length)
                    .WithContentType(contentType);

                await _minioClient.PutObjectAsync(putObjectArgs);

                return filename;
            });
        }

        public async Task<string> UploadFileAsync(UploadGeneratedFileDto request)
            => await UploadFileAsync(request, DefaultUploadPolicy);

        public async Task<string> UploadFileAsync(UploadGeneratedFileDto request, FileUploadPolicy policy)
        {
            if (request.Bytes.Length == 0)
                throw new ArgumentException("File bytes are required.", nameof(request));

            FileUploadValidation.EnsureSizeIsAllowed(request.Bytes.LongLength, policy);

            var contentType = FileUploadValidation.ValidateFileNameAndContentType(
                request.FileName,
                request.ContentType,
                policy);

            return await ExecuteMinioAsync("upload generated file", async () =>
            {
                await EnsureBucketExistsAsync();

                var filename = Guid.NewGuid().ToString();
                await using var fileStream = new MemoryStream(request.Bytes);

                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(_bucket)
                    .WithObject(filename)
                    .WithStreamData(fileStream)
                    .WithObjectSize(fileStream.Length)
                    .WithContentType(contentType);

                await _minioClient.PutObjectAsync(putObjectArgs);

                return filename;
            });
        }

        public Task<string> GenerateFileUrl(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("File token is required.", nameof(token));

            var baseUrl = _publicBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                var request = HttpContextAccessor.HttpContext?.Request
                    ?? throw new InvalidOperationException("HTTP context is not available.");

                baseUrl = $"{request.Scheme}://{request.Host.Value}".TrimEnd('/');
            }

            return Task.FromResult($"{baseUrl}/api/v1/file/getFile?token={Uri.EscapeDataString(token)}");
        }

        public async Task DeleteFileAsync(string token)
        {
            ValidateFileToken(token);

            await ExecuteMinioAsync("delete file", async () =>
            {
                await EnsureBucketExistsAsync();

                var removeObjectArgs = new RemoveObjectArgs()
                    .WithBucket(_bucket)
                    .WithObject(token);

                await _minioClient.RemoveObjectAsync(removeObjectArgs);
            }, token);
        }

        private async Task EnsureBucketExistsAsync()
        {
            if (_bucketExists)
                return;

            await _bucketLock.WaitAsync();
            try
            {
                if (_bucketExists)
                    return;

                if (!await IsBucketExists(_bucket))
                {
                    var makeBucketArgs = new MakeBucketArgs()
                        .WithBucket(_bucket);

                    try
                    {
                        await _minioClient.MakeBucketAsync(makeBucketArgs);
                    }
                    catch
                    {
                        if (!await IsBucketExists(_bucket))
                            throw;
                    }
                }

                _bucketExists = true;
            }
            finally
            {
                _bucketLock.Release();
            }
        }

        private async Task<bool> IsBucketExists(string bucketName)
        {
            var bucketExistsArgs = new BucketExistsArgs().WithBucket(bucketName);
            bool doesBucketExist = await _minioClient.BucketExistsAsync(bucketExistsArgs);
            return doesBucketExist;
        }

        private async Task<string> GetFileContentTypeAsync(string token)
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(_bucket)
                .WithObject(token);

            var status = await _minioClient.StatObjectAsync(statObjectArgs);
            if (status == null)
                throw new NotFoundException(ErrorCode.FileNotFound);

            return status.ContentType;
        }

        private async Task ExecuteMinioAsync(
            string operation,
            Func<Task> action,
            string? token = null)
        {
            await ExecuteMinioAsync(operation, async () =>
            {
                await action();
                return true;
            }, token);
        }

        private async Task<T> ExecuteMinioAsync<T>(
            string operation,
            Func<Task<T>> action,
            string? token = null)
        {
            try
            {
                return await action();
            }
            catch (ObjectNotFoundException exception)
            {
                _logger.LogWarning(
                    exception,
                    "MinIO object was not found during {Operation}. Token: {FileToken}",
                    operation,
                    MaskToken(token));

                throw new NotFoundException(ErrorCode.FileNotFound);
            }
            catch (BucketNotFoundException exception)
            {
                _logger.LogError(
                    exception,
                    "MinIO bucket was not found during {Operation}. Bucket: {BucketName}",
                    operation,
                    _bucket);

                throw new ServiceUnavailableException(ErrorCode.FileStorageUnavailable);
            }
            catch (MinioException exception)
            {
                _logger.LogError(
                    exception,
                    "MinIO failed during {Operation}. Token: {FileToken}",
                    operation,
                    MaskToken(token));

                throw new ServiceUnavailableException(ErrorCode.FileStorageUnavailable);
            }
            catch (Exception exception) when (IsTransientStorageException(exception))
            {
                _logger.LogError(
                    exception,
                    "File storage connection failed during {Operation}. Token: {FileToken}",
                    operation,
                    MaskToken(token));

                throw new ServiceUnavailableException(ErrorCode.FileStorageUnavailable);
            }
        }

        private static bool IsTransientStorageException(Exception exception)
            => exception is HttpRequestException
               or IOException
               or TimeoutException;

        private static void ValidateFileToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token) || !Guid.TryParse(token, out _))
                throw new BadRequestException(ErrorCode.InvalidFileToken);
        }

        private static string MaskToken(string? token)
            => string.IsNullOrWhiteSpace(token) || token.Length <= 8
                ? "***"
                : $"{token[..4]}...{token[^4..]}";

        private static FileUploadPolicy DefaultUploadPolicy => new()
        {
            AllowedExtensions = ".jpg,.jpeg,.png,.webp,.avif,.pdf,.doc,.docx",
            AllowedMimeTypes = "image/jpeg,image/png,image/webp,image/avif,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            MaxSizeMb = 10
        };

        private static async Task ValidateFileSignatureAsync(IFormFile file, string contentType)
        {
            var header = new byte[12];
            await using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(header);
            var signature = header.AsSpan(0, read);

            var isValid = contentType switch
            {
                "image/jpeg" => StartsWith(signature, [0xFF, 0xD8, 0xFF]),
                "image/png" => StartsWith(signature, [0x89, 0x50, 0x4E, 0x47]),
                "image/webp" => StartsWith(signature, [0x52, 0x49, 0x46, 0x46]) &&
                                signature.Length >= 12 &&
                                signature[8] == 0x57 &&
                                signature[9] == 0x45 &&
                                signature[10] == 0x42 &&
                                signature[11] == 0x50,
                "image/avif" => signature.Length >= 12 &&
                                signature[4] == 0x66 &&
                                signature[5] == 0x74 &&
                                signature[6] == 0x79 &&
                                signature[7] == 0x70 &&
                                ((signature[8] == 0x61 &&
                                  signature[9] == 0x76 &&
                                  signature[10] == 0x69 &&
                                  signature[11] == 0x66) ||
                                 (signature[8] == 0x61 &&
                                  signature[9] == 0x76 &&
                                  signature[10] == 0x69 &&
                                  signature[11] == 0x73)),
                "application/pdf" => StartsWith(signature, [0x25, 0x50, 0x44, 0x46]),
                "application/msword" => StartsWith(signature, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" =>
                    StartsWith(signature, [0x50, 0x4B, 0x03, 0x04]) ||
                    StartsWith(signature, [0x50, 0x4B, 0x05, 0x06]) ||
                    StartsWith(signature, [0x50, 0x4B, 0x07, 0x08]),
                _ => false
            };

            if (!isValid)
                throw new BusinessRuleException(ErrorCode.FileContentTypeMismatch);
        }

        private static bool StartsWith(ReadOnlySpan<byte> source, ReadOnlySpan<byte> expected)
            => source.Length >= expected.Length && source[..expected.Length].SequenceEqual(expected);
    }
}
