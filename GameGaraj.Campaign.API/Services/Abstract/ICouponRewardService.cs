using GameGaraj.Campaign.API.Models;

namespace GameGaraj.Campaign.API.Services.Abstract
{
    public interface ICouponRewardService
    {
        Task<List<CouponRewardRule>> GetAllAsync();
        Task<List<CouponRewardRule>> GetActiveAsync();
        Task<CouponRewardRule?> GetByIdAsync(int id);
        Task<bool> SaveAsync(CouponRewardRule rule);
        Task<bool> UpdateAsync(CouponRewardRule rule);
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Kullanıcının alışveriş geçmişini kontrol edip eşleşen ödül kuralları için kupon oluşturur.
        /// </summary>
        Task<List<Coupon>> CheckAndGrantRewardsAsync(string userId);

        /// <summary>Kullanıcının alışveriş kaydını ekler.</summary>
        Task AddPurchaseLogAsync(string userId, int orderId, decimal totalAmount);

        /// <summary>Kullanıcının belirli süre içindeki toplam alışveriş tutarını hesaplar.</summary>
        Task<decimal> GetUserSpendInPeriodAsync(string userId, int days);
    }
}
