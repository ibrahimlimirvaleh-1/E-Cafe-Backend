using AutoMapper;
using ECafe.Application.DTOs.File;
using ECafe.Application.Services;
using ECafe.Application.Services.MinIO.Abstracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using ECafe.Shared.Extensions;

namespace ECafe.Infrastructure.Services.MinIO
{
    public class MinioManager : BaseManager, IMinioService
    {
        private readonly IMinioClient _minioClient;
        private readonly string? _bucket;
        private readonly string? _endpoint;
        private readonly string? _accessKey;
        private readonly string? _secretKey;

        public MinioManager(IHttpContextAccessor httpContextAccessor,
                            IMapper mapper,
                            IConfiguration configuration)
                            : base(httpContextAccessor, mapper, configuration)
        {
            _bucket = configuration["Minio:BucketName"];
            _endpoint = configuration["Minio:Endpoint"];
            _accessKey = configuration["Minio:AccessKey"];
            _secretKey = configuration["Minio:SecretKey"];

            _minioClient = new MinioClient()
            .WithEndpoint(_endpoint)
            .WithCredentials(_accessKey, _secretKey)
            .WithSSL(false)
            .Build();
        }



        public async Task<GetFileResponse> GetFileAsync(string token)
        {
            if (!await IsBucketExists(_bucket))
                throw new Exception("NotFound");

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
            if (!await IsBucketExists(_bucket))
                throw new Exception("NotFound");

            var filestream = new MemoryStream(await request.File.GetBytes());
            var filename = Guid.NewGuid().ToString();

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
            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.ToLowerInvariant();

            var host = HttpContextAccessor.HttpContext.Request.Host.Value;

            return $"http://{host}/api/file/getFile?token={token}";
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
