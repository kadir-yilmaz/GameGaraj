namespace GameGaraj.WebUI.Services.Abstract
{
    public interface IFavoritesService
    {
        Task<List<string>> GetFavoriteProductIdsAsync();
        Task<bool> AddFavoriteAsync(string productId);
        Task<bool> RemoveFavoriteAsync(string productId);
        Task<bool> ToggleFavoriteAsync(string productId);
        Task<bool> IsFavoriteAsync(string productId);
    }
}
