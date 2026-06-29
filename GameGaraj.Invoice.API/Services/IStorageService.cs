using System.Threading;
using System.Threading.Tasks;

namespace GameGaraj.Invoice.API.Services
{
    public interface IStorageService
    {
        /// <summary>
        /// Uploads file bytes to storage and returns its relative path/object name (e.g. "invoices/filename.ext")
        /// </summary>
        Task<string> UploadFileAsync(byte[] fileBytes, string fileName, string contentType, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes a file from storage by its relative path/object name
        /// </summary>
        Task DeleteFileAsync(string fileName, CancellationToken cancellationToken);
    }
}
