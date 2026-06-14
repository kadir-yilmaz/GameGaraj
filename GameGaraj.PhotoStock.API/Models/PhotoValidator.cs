using Microsoft.AspNetCore.Http;

namespace GameGaraj.PhotoStock.API.Models
{
    public static class PhotoValidator
    {
        public static string? Validate(IFormFile file)
        {
            if (file.Length > PhotoLimits.MaxFileSize)
                return $"{file.FileName}: Max 5MB olabilir.";

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!PhotoLimits.AllowedExtensions.Contains(ext))
                return $"{file.FileName}: Geçersiz format.";

            return null;
        }
    }
}
