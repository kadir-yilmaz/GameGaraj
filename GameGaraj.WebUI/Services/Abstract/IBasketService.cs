using GameGaraj.WebUI.Models.Baskets;

namespace GameGaraj.WebUI.Services.Abstract
{
    public interface IBasketService
    {
        Task<BasketViewModel?> GetBasketAsync();
        Task<bool> SaveOrUpdateAsync(BasketViewModel basket);
        Task<bool> DeleteAsync();
        Task<bool> AddItemAsync(BasketItemViewModel item);
        Task<bool> RemoveItemAsync(string productId);
        Task SyncBasketAsync(string guestId, string userId);
    }
}
