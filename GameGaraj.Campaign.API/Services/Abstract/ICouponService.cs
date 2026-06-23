using GameGaraj.Campaign.API.Models;

namespace GameGaraj.Campaign.API.Services.Abstract
{
    public interface ICouponService
    {
        Task<List<Coupon>> GetAllAsync();
        Task<Coupon?> GetByIdAsync(int id);
        Task<Coupon?> GetByCodeAsync(string code);
        Task<List<Coupon>> GetPublicCouponsAsync();
        Task<List<Coupon>> GetByUserIdAsync(string userId);
        Task<bool> SaveAsync(Coupon coupon);
        Task<bool> UpdateAsync(Coupon coupon);
        Task<bool> DeleteAsync(int id);
        Task<bool> MarkAsUsedAsync(int id);
    }
}
