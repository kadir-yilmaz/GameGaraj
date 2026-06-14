using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace GameGaraj.PhotoStock.API.Services
{
    public class LocalStorageService : IStorageService
    {
        private readonly string _photosFolder;

        public LocalStorageService(IWebHostEnvironment environment)
        {
            var wwwrootPath = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
            _photosFolder = Path.Combine(wwwrootPath, "photos");
            if (!Directory.Exists(_photosFolder))
            {
                Directory.CreateDirectory(_photosFolder);
            }
        }

        public async Task<string> UploadFileAsync(IFormFile file, string fileName, CancellationToken cancellationToken)
        {
            var filePath = Path.Combine(_photosFolder, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);
            return $"photos/{fileName}";
        }

        public Task DeleteFileAsync(string fileName, CancellationToken cancellationToken)
        {
            var safeFileName = Path.GetFileName(fileName);
            var filePath = Path.Combine(_photosFolder, safeFileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return Task.CompletedTask;
        }
    }
}
