using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;

namespace GameGaraj.PhotoStock.API.Services
{
    public class MinioStorageService : IStorageService
    {
        private readonly IMinioClient _minioClient;
        private readonly string _bucketName;
        private bool _bucketInitialized = false;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public MinioStorageService(IMinioClient minioClient, IConfiguration configuration)
        {
            _minioClient = minioClient;
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
                    var makeArgs = new MakeBucketArgs().WithBucket(_bucketName);
                    await _minioClient.MakeBucketAsync(makeArgs, cancellationToken);

                    // Set bucket read policy to public so clients can download directly
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
                }
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

            var putArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(file.Length)
                .WithContentType(file.ContentType);

            await _minioClient.PutObjectAsync(putArgs, cancellationToken);

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
    }
}
