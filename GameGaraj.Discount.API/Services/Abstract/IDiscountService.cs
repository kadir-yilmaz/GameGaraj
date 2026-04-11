namespace GameGaraj.Discount.API.Services.Abstract
{
    public interface IDiscountService
    {
        Task<List<Models.Discount>> GetAllAsync();
        Task<List<Models.Discount>> GetAllByUserIdAsync(string userId);
        Task<Models.Discount?> GetByIdAsync(int id);
        Task<bool> SaveAsync(Models.Discount discount);
        Task<bool> UpdateAsync(Models.Discount discount);
        Task<bool> DeleteAsync(int id);
        Task<Models.Discount?> GetByCodeAndUserIdAsync(string code, string userId);
        Task<Models.Discount?> GetByCodeAsync(string code);
    }
}
