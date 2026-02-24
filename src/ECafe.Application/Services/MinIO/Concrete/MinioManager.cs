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

        private async Task<bool> IsBucketExists(string bucketName)
        {
            var bucketExistsArgs = new BucketExistsArgs().WithBucket(bucketName);
            bool doesBucketExist = await _minioClient.BucketExistsAsync(bucketExistsArgs);
            return doesBucketExist;
        }
    }
}
