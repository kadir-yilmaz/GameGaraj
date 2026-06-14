using GameGaraj.PhotoStock.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.PhotoStock.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PhotosController : ControllerBase
    {
        private readonly IStorageService _storageService;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const int MaxPhotos = 5;
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

        public PhotosController(IStorageService storageService)
        {
            _storageService = storageService;
        }

        /// <summary>
        /// Upload multiple photos (max 5)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UploadPhotos([FromForm] List<IFormFile> photos, CancellationToken cancellationToken)
        {
            // Validation: Check if any photos provided
            if (photos == null || photos.Count == 0)
            {
                return BadRequest(new { error = "En az bir resim yüklemelisiniz." });
            }

            // Validation: Max 5 photos
            if (photos.Count > MaxPhotos)
            {
                return BadRequest(new { error = $"Maksimum {MaxPhotos} resim yükleyebilirsiniz." });
            }

            var uploadedUrls = new List<string>();
            var errors = new List<string>();

            foreach (var photo in photos)
            {
                // Validation: File size
                if (photo.Length > MaxFileSize)
                {
                    errors.Add($"{photo.FileName}: Dosya boyutu 5MB'dan büyük olamaz.");
                    continue;
                }

                // Validation: File extension
                var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
                if (!_allowedExtensions.Contains(extension))
                {
                    errors.Add($"{photo.FileName}: Sadece JPG, PNG ve WebP formatları kabul edilir.");
                    continue;
                }

                try
                {
                    // Generate unique filename: GUID + original extension
                    var fileName = $"{Guid.NewGuid()}{extension}";
                    var relativePath = await _storageService.UploadFileAsync(photo, fileName, cancellationToken);
                    uploadedUrls.Add(relativePath);
                }
                catch (Exception ex)
                {
                    errors.Add($"{photo.FileName}: Yükleme sırasında hata oluştu. Hata: {ex.Message}");
                }
            }

            // If all files failed
            if (uploadedUrls.Count == 0 && errors.Count > 0)
            {
                return BadRequest(new { errors });
            }

            return Ok(new
            {
                urls = uploadedUrls,
                errors = errors.Count > 0 ? errors : null
            });
        }

        /// <summary>
        /// Delete a photo by filename
        /// </summary>
        [HttpDelete("{fileName}")]
        public async Task<IActionResult> DeletePhoto(string fileName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return BadRequest(new { error = "Dosya adı gereklidir." });
            }

            try
            {
                await _storageService.DeleteFileAsync(fileName, cancellationToken);
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Silme hatası: {ex.Message}" });
            }
        }

        /// <summary>
        /// Delete multiple photos
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> DeletePhotos([FromBody] List<string> fileNames, CancellationToken cancellationToken)
        {
            if (fileNames == null || fileNames.Count == 0)
            {
                return BadRequest(new { error = "Silinecek dosya adları gereklidir." });
            }

            var deleted = new List<string>();
            var errors = new List<string>();

            foreach (var fileName in fileNames)
            {
                try
                {
                    await _storageService.DeleteFileAsync(fileName, cancellationToken);
                    deleted.Add(fileName);
                }
                catch (Exception ex)
                {
                    errors.Add($"{fileName}: Silme hatası. Hata: {ex.Message}");
                }
            }

            return Ok(new
            {
                deleted,
                errors = errors.Count > 0 ? errors : null
            });
        }
    }
}
