using GameGaraj.WebUI.Models.Discounts;

namespace GameGaraj.WebUI.Services.Abstract
{
    public interface IDiscountService
    {
        Task<List<DiscountViewModel>> GetAllAsync();
        Task<DiscountViewModel?> GetByIdAsync(int id);
        Task<bool> SaveAsync(CreateDiscountInput input);
        Task<bool> UpdateAsync(UpdateDiscountInput input);
        Task<bool> DeleteAsync(int id);
        Task<DiscountViewModel?> GetByCodeAsync(string code);
    }
}
