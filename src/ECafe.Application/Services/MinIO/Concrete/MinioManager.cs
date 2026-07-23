using AutoMapper;
using ECafe.Application.DTOs.File;
using ECafe.Application.Services;
using ECafe.Application.Services.MinIO.Abstracts;
using ECafe.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace ECafe.Infrastructure.Services.MinIO
{
    public class MinioManager : BaseManager, IMinioService
    {
        private readonly IMinioClient _minioClient;
        private readonly string _bucket;
        private readonly string? _publicBaseUrl;
        private bool _bucketExists;
        private readonly SemaphoreSlim _bucketLock = new(1, 1);
        private const long MaxUploadSize = 10 * 1024 * 1024;
        private static readonly Dictionary<string, string[]> AllowedUploadTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = [".jpg", ".jpeg"],
            ["image/png"] = [".png"],
            ["image/webp"] = [".webp"],
            ["application/pdf"] = [".pdf"],
            ["application/msword"] = [".doc"],
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = [".docx"]
        };

        public MinioManager(IHttpContextAccessor httpContextAccessor,
                            IMapper mapper,
                            IConfiguration configuration)
                            : base(httpContextAccessor, mapper, configuration)
        {
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
            await EnsureBucketExistsAsync();

            var contentType = await IsFileExists(token);

            var destination = new MemoryStream();

            var getObjectArgs = new GetObjectArgs()
                .WithBucket(_bucket)
                .WithObject(token)
                .WithCallbackStream((stream) => { stream.CopyTo(destination); });

            await _minioClient.GetObjectAsync(getObjectArgs);

            return new GetFileResponse(destination.ToArray(), contentType);

        }

        public async Task<string> UploadFileAsync(UploadFileDto request)
        {
            if (request.File is null || request.File.Length == 0)
                throw new ArgumentException("File is required.", nameof(request));

            if (request.File.Length > MaxUploadSize)
                throw new BusinessRuleException("File size must not exceed 10 MB.");

            var contentType = ValidateUploadType(request.File.FileName, request.File.ContentType);
            await ValidateFileSignatureAsync(request.File, contentType);

            await EnsureBucketExistsAsync();

            var filename = Guid.NewGuid().ToString();
            await using var filestream = request.File.OpenReadStream();

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_bucket)
                .WithObject(filename)
                .WithStreamData(filestream)
                .WithObjectSize(filestream.Length)
                .WithContentType(request.File.ContentType);

            await _minioClient.PutObjectAsync(putObjectArgs);

            return filename;
        }

        public async Task<string> UploadFileAsync(UploadGeneratedFileDto request)
        {
            if (request.Bytes.Length == 0)
                throw new ArgumentException("File bytes are required.", nameof(request));

            ValidateUploadType(request.FileName, request.ContentType);

            await EnsureBucketExistsAsync();

            var filename = Guid.NewGuid().ToString();
            await using var fileStream = new MemoryStream(request.Bytes);

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_bucket)
                .WithObject(filename)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(request.ContentType);

            await _minioClient.PutObjectAsync(putObjectArgs);

            return filename;
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
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("File token is required.", nameof(token));

            await EnsureBucketExistsAsync();

            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_bucket)
                .WithObject(token);

            await _minioClient.RemoveObjectAsync(removeObjectArgs);
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

        private async Task<string> IsFileExists(string token)
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(_bucket)
                .WithObject(token);

            var status = await _minioClient.StatObjectAsync(statObjectArgs);
            if (status == null)
                throw new Exception("File not found or deleted");

            return status.ContentType;
        }

        private static string ValidateUploadType(string fileName, string? contentType)
        {
            var normalizedContentType = NormalizeContentType(contentType);
            if (!AllowedUploadTypes.TryGetValue(normalizedContentType, out var extensions))
                throw new BusinessRuleException("Unsupported file type.");

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrWhiteSpace(extension) ||
                !extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                throw new BusinessRuleException("File extension does not match the allowed MIME type.");

            return normalizedContentType;
        }

        private static string NormalizeContentType(string? contentType)
            => string.IsNullOrWhiteSpace(contentType)
                ? string.Empty
                : contentType.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)[0];

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
                "application/pdf" => StartsWith(signature, [0x25, 0x50, 0x44, 0x46]),
                "application/msword" => StartsWith(signature, [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" =>
                    StartsWith(signature, [0x50, 0x4B, 0x03, 0x04]) ||
                    StartsWith(signature, [0x50, 0x4B, 0x05, 0x06]) ||
                    StartsWith(signature, [0x50, 0x4B, 0x07, 0x08]),
                _ => false
            };

            if (!isValid)
                throw new BusinessRuleException("File content does not match the declared MIME type.");
        }

        private static bool StartsWith(ReadOnlySpan<byte> source, ReadOnlySpan<byte> expected)
            => source.Length >= expected.Length && source[..expected.Length].SequenceEqual(expected);
    }
}
