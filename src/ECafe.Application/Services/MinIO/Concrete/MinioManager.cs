using AutoMapper;
using ECafe.Application.DTOs.File;
using ECafe.Application.Services;
using ECafe.Application.Services.MinIO.Abstracts;
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
        private bool _bucketExists;
        private readonly SemaphoreSlim _bucketLock = new(1, 1);

        public MinioManager(IHttpContextAccessor httpContextAccessor,
                            IMapper mapper,
                            IConfiguration configuration)
                            : base(httpContextAccessor, mapper, configuration)
        {
            _bucket = configuration["Minio:BucketName"]
                ?? throw new InvalidOperationException("Minio:BucketName is not configured.");
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

        public async Task<string> GenerateFileUrl(string token)
        {
            var host = HttpContextAccessor.HttpContext?.Request.Host.Value
                ?? throw new InvalidOperationException("HTTP context is not available.");

            return $"http://{host}/api/file/getFile?token={token}";
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
    }
}
