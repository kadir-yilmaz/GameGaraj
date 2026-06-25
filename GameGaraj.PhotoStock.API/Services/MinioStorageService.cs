using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using GameGaraj.PhotoStock.API.Models;

namespace GameGaraj.PhotoStock.API.Services
{
    public class MinioStorageService : IStorageService
    {
        private readonly IMinioClient _minioClient;
        private readonly string _bucketName;
        private readonly ILogger<MinioStorageService> _logger;
        private bool _bucketInitialized = false;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public MinioStorageService(
            IMinioClient minioClient,
            IConfiguration configuration,
            ILogger<MinioStorageService> logger)
        {
            _minioClient = minioClient;
            _logger = logger;
            _bucketName = configuration["Minio:BucketName"] ?? "gamegaraj";
        }

        private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
        {
            if (_bucketInitialized) return;

            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_bucketInitialized) return;

                var existsArgs = new BucketExistsArgs().WithBucket(_bucketName);
                bool exists = await _minioClient.BucketExistsAsync(existsArgs, cancellationToken);
                if (!exists)
                {
                    _logger.LogInformation("MinIO bucket {BucketName} does not exist. Creating it.", _bucketName);

                    var makeArgs = new MakeBucketArgs().WithBucket(_bucketName);
                    await _minioClient.MakeBucketAsync(makeArgs, cancellationToken);
                }

                // Ensure existing buckets are also readable by the browser.
                var policyJson = $@"{{
                    ""Version"": ""2012-10-17"",
                    ""Statement"": [
                        {{
                            ""Effect"": ""Allow"",
                            ""Principal"": ""*"",
                            ""Action"": [""s3:GetObject""],
                            ""Resource"": [""arn:aws:s3:::{_bucketName}/*""]
                        }}
                    ]
                }}";
                var policyArgs = new SetPolicyArgs().WithBucket(_bucketName).WithPolicy(policyJson);
                await _minioClient.SetPolicyAsync(policyArgs, cancellationToken);

                _bucketInitialized = true;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<string> UploadFileAsync(IFormFile file, string fileName, CancellationToken cancellationToken)
        {
            await EnsureBucketExistsAsync(cancellationToken);

            var objectName = $"photos/{fileName}";
            using var stream = file.OpenReadStream();

            _logger.LogInformation(
                "Uploading photo to MinIO bucket {BucketName} as object {ObjectName}. ContentType: {ContentType}, Size: {Size}",
                _bucketName,
                objectName,
                file.ContentType,
                file.Length);

            var putArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(file.Length)
                .WithContentType(file.ContentType);

            await _minioClient.PutObjectAsync(putArgs, cancellationToken);

            var statArgs = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName);
            var stat = await _minioClient.StatObjectAsync(statArgs, cancellationToken);

            _logger.LogInformation(
                "Photo uploaded and verified in MinIO bucket {BucketName} as object {ObjectName}. Size: {Size}",
                _bucketName,
                objectName,
                stat.Size);

            return objectName;
        }

        public async Task DeleteFileAsync(string fileName, CancellationToken cancellationToken)
        {
            await EnsureBucketExistsAsync(cancellationToken);

            var safeFileName = Path.GetFileName(fileName);
            var objectName = $"photos/{safeFileName}";

            var removeArgs = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeArgs, cancellationToken);
        }

        public async Task<StorageHealthResult> CheckHealthAsync(CancellationToken cancellationToken)
        {
            try
            {
                var existsArgs = new BucketExistsArgs().WithBucket(_bucketName);
                var exists = await _minioClient.BucketExistsAsync(existsArgs, cancellationToken);

                return new StorageHealthResult(
                    exists,
                    "MinIO",
                    _bucketName,
                    exists
                        ? $"MinIO bucket '{_bucketName}' is reachable."
                        : $"MinIO is reachable, but bucket '{_bucketName}' does not exist.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MinIO storage health check failed for bucket {BucketName}", _bucketName);

                return new StorageHealthResult(
                    false,
                    "MinIO",
                    _bucketName,
                    ex.Message);
            }
        }
    }
}
