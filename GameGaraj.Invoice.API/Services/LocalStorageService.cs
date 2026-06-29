using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace GameGaraj.Invoice.API.Services
{
    public class LocalStorageService : IStorageService
    {
        private readonly string _invoicesFolder;

        public LocalStorageService(IWebHostEnvironment environment)
        {
            var wwwrootPath = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
            _invoicesFolder = Path.Combine(wwwrootPath, "invoices");
            Directory.CreateDirectory(_invoicesFolder);
        }

        public async Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string contentType, CancellationToken cancellationToken)
        {
            var safeFileName = Path.GetFileName(fileName);
            var filePath = Path.Combine(_invoicesFolder, safeFileName);

            await File.WriteAllBytesAsync(filePath, fileBytes, cancellationToken);

            return $"invoices/{safeFileName}";
        }

        public Task DeleteFileAsync(string fileName, CancellationToken cancellationToken)
        {
            var safeFileName = Path.GetFileName(fileName);
            var filePath = Path.Combine(_invoicesFolder, safeFileName);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            return Task.CompletedTask;
        }
    }
}
