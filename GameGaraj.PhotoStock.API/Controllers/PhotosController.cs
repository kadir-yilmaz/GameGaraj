using GameGaraj.PhotoStock.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace GameGaraj.PhotoStock.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PhotosController : ControllerBase
{
    private readonly IStorageService _storageService;

    public PhotosController(IStorageService storageService)
    {
        _storageService = storageService;
    }

    private const int MaxPhotos = 5;
    private const long MaxFileSize = 5 * 1024 * 1024;

    private static readonly string[] AllowedExtensions =
        { ".jpg", ".jpeg", ".png", ".webp" };


    [HttpPost]
    public async Task<IActionResult> UploadPhotos(
        [FromForm] List<IFormFile> photos,
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
                var fileName = $"{Guid.NewGuid()}{ext}";

                var url = await _storageService.UploadFileAsync(
                    photo,
                    fileName,
                    cancellationToken);

                urls.Add(url);
            }
            catch (Exception ex)
            {
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
                errors.Add($"{fileName}: {ex.Message}");
            }
        }

        return Ok(new { deleted, errors = errors.Count == 0 ? null : errors });
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