namespace GameGaraj.PhotoStock.API.Models
{
    public class UploadPhotoResponse
    {
        public List<string> Urls { get; set; } = new();
        public List<string>? Errors { get; set; }
    }
}
