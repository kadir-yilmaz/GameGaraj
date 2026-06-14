using Microsoft.AspNetCore.Http;

namespace GameGaraj.WebUI.Services.Abstract
{
    public interface IPhotoStockService
    {
        Task<List<string>> UploadPhotosAsync(IFormFileCollection photos, string? brand, string? productName);
        Task<bool> DeletePhotoAsync(string photoUrl);
    }
}
