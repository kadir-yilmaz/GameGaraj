using GameGaraj.PhotoStock.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace GameGaraj.PhotoStock.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PhotosController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly ILogger<PhotosController> _logger;

    public PhotosController(IStorageService storageService, ILogger<PhotosController> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    private const int MaxPhotos = 5;
    private const long MaxFileSize = 5 * 1024 * 1024;

    private static readonly string[] AllowedExtensions =
        { ".jpg", ".jpeg", ".png", ".webp" };


    [HttpPost]
    public async Task<IActionResult> UploadPhotos(
        [FromForm] List<IFormFile> photos,
        [FromForm] string? brand,
        [FromForm] string? productName,
        CancellationToken cancellationToken)
    {
        if (photos == null || photos.Count == 0)
            return BadRequest("En az bir resim gerekli.");

        if (photos.Count > MaxPhotos)
            return BadRequest($"Max {MaxPhotos} resim yüklenebilir.");

        var urls = new List<string>();
        var errors = new List<string>();

        foreach (var photo in photos)
        {
            var validationError = Validate(photo);
            if (validationError != null)
            {
                errors.Add(validationError);
                continue;
            }

            try
            {
                var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
                var fileName = BuildFileName(brand, productName, ext);

                var url = await _storageService.UploadFileAsync(
                    photo,
                    fileName,
                    cancellationToken);

                urls.Add(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Photo upload failed for file {FileName}. ContentType: {ContentType}, Size: {Size}",
                    photo.FileName,
                    photo.ContentType,
                    photo.Length);
                errors.Add($"{photo.FileName}: {ex.Message}");
            }
        }

        if (urls.Count == 0)
            return BadRequest(new { urls, errors });

        return Ok(new { urls, errors = errors.Count == 0 ? null : errors });
    }

    [HttpDelete("{fileName}")]
    public async Task<IActionResult> DeletePhoto(
        string fileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest("Dosya adı gerekli.");

        try
        {
            await _storageService.DeleteFileAsync(fileName, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Photo delete failed for file {FileName}", fileName);
            return StatusCode(500, ex.Message);
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeletePhotos(
        [FromBody] List<string> fileNames,
        CancellationToken cancellationToken)
    {
        if (fileNames == null || fileNames.Count == 0)
            return BadRequest("Silinecek dosya yok.");

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
                _logger.LogError(ex, "Photo delete failed for file {FileName}", fileName);
                errors.Add($"{fileName}: {ex.Message}");
            }
        }

        return Ok(new { deleted, errors = errors.Count == 0 ? null : errors });
    }

    private static string BuildFileName(string? brand, string? productName, string extension)
    {
        var prefixParts = new[]
        {
            ToSlug(brand),
            ToSlug(productName)
        }.Where(part => !string.IsNullOrWhiteSpace(part));

        var prefix = string.Join("-", prefixParts);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "product";
        }

        var randomId = Guid.NewGuid().ToString("N")[..10];
        return $"{prefix}-{randomId}{extension}";
    }

    private static string ToSlug(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim()
            .Replace('ı', 'i')
            .Replace('İ', 'I')
            .Replace('ğ', 'g')
            .Replace('Ğ', 'G')
            .Replace('ü', 'u')
            .Replace('Ü', 'U')
            .Replace('ş', 's')
            .Replace('Ş', 'S')
            .Replace('ö', 'o')
            .Replace('Ö', 'O')
            .Replace('ç', 'c')
            .Replace('Ç', 'C')
            .Normalize(NormalizationForm.FormD);

        var builder = new StringBuilder(normalized.Length);
        var previousDash = false;

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousDash = false;
                continue;
            }

            if (!previousDash && builder.Length > 0)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        return builder.ToString().Trim('-');
    }

    private string? Validate(IFormFile file)
    {
        if (file.Length > MaxFileSize)
            return $"{file.FileName}: Max 5MB olabilir.";

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(ext))
            return $"{file.FileName}: Geçersiz format.";

        return null;
    }
}
