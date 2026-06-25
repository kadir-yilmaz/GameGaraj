using Microsoft.AspNetCore.Http;
using GameGaraj.PhotoStock.API.Models;

namespace GameGaraj.PhotoStock.API.Services
{
    public interface IStorageService
    {
        /// <summary>
        /// Uploads a file to storage and returns its relative path (e.g. "photos/filename.ext")
        /// </summary>
        Task<string> UploadFileAsync(IFormFile file, string fileName, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes a file from storage by its filename (e.g. "filename.ext" or "photos/filename.ext")
        /// </summary>
        Task DeleteFileAsync(string fileName, CancellationToken cancellationToken);

        /// <summary>
        /// Checks whether the configured storage backend is reachable.
        /// </summary>
        Task<StorageHealthResult> CheckHealthAsync(CancellationToken cancellationToken);
    }
}
