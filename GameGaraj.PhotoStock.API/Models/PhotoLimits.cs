namespace GameGaraj.PhotoStock.API.Models
{
    public static class PhotoLimits
    {
        public const int MaxPhotos = 5;
        public const long MaxFileSize = 5 * 1024 * 1024;

        public static readonly string[] AllowedExtensions =
            { ".jpg", ".jpeg", ".png", ".webp" };
    }
}
