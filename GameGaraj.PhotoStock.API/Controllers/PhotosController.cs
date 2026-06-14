using GameGaraj.PhotoStock.API.Services;
using Microsoft.AspNetCore.Mvc;

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

    [HttpPost]
    public async Task<IActionResult> UploadPhotos(
        [FromForm] List<IFormFile> photos,
        CancellationToken cancellationToken)
    {
        if (photos == null || photos.Count == 0)
            return BadRequest("En az bir resim gerekli.");

        if (photos.Count > PhotoLimits.MaxPhotos)
            return BadRequest($"Max {PhotoLimits.MaxPhotos} resim yüklenebilir.");

        var response = new UploadPhotoResponse();

        foreach (var photo in photos)
        {
            var validationError = PhotoValidator.Validate(photo);
            if (validationError != null)
            {
                response.Errors ??= new List<string>();
                response.Errors.Add(validationError);
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

                response.Urls.Add(url);
            }
            catch (Exception ex)
            {
                response.Errors ??= new List<string>();
                response.Errors.Add($"{photo.FileName}: {ex.Message}");
            }
        }

        if (response.Urls.Count == 0)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpDelete("{fileName}")]
    public async Task<IActionResult> DeletePhoto(string fileName, CancellationToken cancellationToken)
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

        var result = new
        {
            deleted = new List<string>(),
            errors = new List<string>()
        };

        foreach (var fileName in fileNames)
        {
            try
            {
                await _storageService.DeleteFileAsync(fileName, cancellationToken);
                result.deleted.Add(fileName);
            }
            catch (Exception ex)
            {
                result.errors.Add($"{fileName}: {ex.Message}");
            }
        }

        return Ok(result);
    }
}