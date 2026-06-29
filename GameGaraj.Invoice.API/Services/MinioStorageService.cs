using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace GameGaraj.Invoice.API.Services
{
    public class MinioStorageService : IStorageService
    {
        private readonly IMinioClient _minioClient;
        private readonly string _bucketName;
        private readonly ILogger<MinioStorageService> _logger;
        private bool _bucketInitialized = false;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

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

                // Ensure bucket is publicly readable
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

        public async Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string contentType, CancellationToken cancellationToken)
        {
            await EnsureBucketExistsAsync(cancellationToken);

            var objectName = $"invoices/{fileName}";
            using var stream = new MemoryStream(fileBytes);

            _logger.LogInformation(
                "Uploading invoice to MinIO bucket {BucketName} as object {ObjectName}. ContentType: {ContentType}, Size: {Size}",
                _bucketName,
                objectName,
                contentType,
                fileBytes.Length);

            var putArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(fileBytes.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putArgs, cancellationToken);

            // Verify upload
            var statArgs = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName);
            var stat = await _minioClient.StatObjectAsync(statArgs, cancellationToken);

            _logger.LogInformation(
                "Invoice uploaded and verified in MinIO bucket {BucketName} as object {ObjectName}. Size: {Size}",
                _bucketName,
                objectName,
                stat.Size);

            return objectName;
        }

        public async Task DeleteFileAsync(string fileName, CancellationToken cancellationToken)
        {
            await EnsureBucketExistsAsync(cancellationToken);

            var safeFileName = Path.GetFileName(fileName);
            var objectName = $"invoices/{safeFileName}";

            var removeArgs = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeArgs, cancellationToken);
            _logger.LogInformation("Deleted object {ObjectName} from MinIO", objectName);
        }
    }
}
