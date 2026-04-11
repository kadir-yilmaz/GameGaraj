using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.PhotoStock.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PhotosController : ControllerBase
    {
        private readonly string _wwwrootPath;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const int MaxPhotos = 5;
        private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

        public PhotosController(IWebHostEnvironment environment)
        {
            _wwwrootPath = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
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

            var photosFolder = Path.Combine(_wwwrootPath, "photos");
            if (!Directory.Exists(photosFolder))
            {
                Directory.CreateDirectory(photosFolder);
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

                // Generate unique filename: GUID + original extension
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(photosFolder, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await photo.CopyToAsync(stream, cancellationToken);

                uploadedUrls.Add($"photos/{fileName}");
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
        public IActionResult DeletePhoto(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return BadRequest(new { error = "Dosya adı gereklidir." });
            }

            // Security: Prevent directory traversal
            fileName = Path.GetFileName(fileName);

            var filePath = Path.Combine(_wwwrootPath, "photos", fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { error = "Resim bulunamadı." });
            }

            System.IO.File.Delete(filePath);

            return NoContent();
        }

        /// <summary>
        /// Delete multiple photos
        /// </summary>
        [HttpDelete]
        public IActionResult DeletePhotos([FromBody] List<string> fileNames)
        {
            if (fileNames == null || fileNames.Count == 0)
            {
                return BadRequest(new { error = "Silinecek dosya adları gereklidir." });
            }

            var deleted = new List<string>();
            var notFound = new List<string>();

            foreach (var fileName in fileNames)
            {
                // Security: Prevent directory traversal
                var safeFileName = Path.GetFileName(fileName);
                var filePath = Path.Combine(_wwwrootPath, "photos", safeFileName);

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    deleted.Add(safeFileName);
                }
                else
                {
                    notFound.Add(safeFileName);
                }
            }

            return Ok(new
            {
                deleted,
                notFound = notFound.Count > 0 ? notFound : null
            });
        }
    }
}
