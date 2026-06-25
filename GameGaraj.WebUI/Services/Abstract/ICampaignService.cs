using GameGaraj.WebUI.Models.Campaigns;

namespace GameGaraj.WebUI.Services.Abstract
{
    /// <summary>
    /// Campaign API ile iletişim için servis arayüzü.
    /// Admin panel CRUD ve sepet indirim hesaplama işlemlerini kapsar.
    /// </summary>
    public interface ICampaignService
    {
        // CRUD — Admin Panel
        Task<List<CampaignRuleViewModel>> GetAllRulesAsync();
        Task<CampaignRuleViewModel?> GetRuleByIdAsync(int id);
        Task<bool> CreateRuleAsync(CampaignRuleCreateInput input);
        Task<bool> UpdateRuleAsync(CampaignRuleUpdateInput input);
        Task<bool> DeleteRuleAsync(int id);

        // İndirim Hesaplama — WebUI Sepet / Checkout
        Task<CalculateDiscountResponse?> CalculateDiscountAsync(CalculateDiscountRequest request);

        // Kargo Ayarları
        Task<ShippingSettingViewModel?> GetShippingSettingAsync();
        Task<bool> UpdateShippingSettingAsync(ShippingSettingViewModel input);

        // --- KUPON YÖNETİMİ ---
        Task<List<CouponViewModel>> GetAllCouponsAsync();
        Task<CouponViewModel?> GetCouponByIdAsync(int id);
        Task<CouponViewModel?> GetCouponByCodeAsync(string code);
        Task<List<CouponViewModel>> GetPublicCouponsAsync();
        Task<List<CouponViewModel>> GetUserCouponsAsync(string userId);
        Task<bool> CreateCouponAsync(CouponCreateInput input);
        Task<bool> DeleteCouponAsync(int id);
        Task<bool> MarkCouponAsUsedAsync(string code);

        // --- KUPON KAZAN KURALLARI ---
        Task<List<CouponRewardRuleViewModel>> GetAllRewardRulesAsync();
        Task<CouponRewardRuleViewModel?> GetRewardRuleByIdAsync(int id);
        Task<bool> CreateRewardRuleAsync(CouponRewardRuleCreateInput input);
        Task<bool> UpdateRewardRuleAsync(CouponRewardRuleViewModel input);
        Task<bool> DeleteRewardRuleAsync(int id);

        // --- BİLDİRİMLER ---
        Task<List<NotificationViewModel>> GetNotificationsAsync(string userId, bool unreadOnly = false);
        Task<int> GetUnreadNotificationCountAsync(string userId);
        Task<bool> MarkNotificationAsReadAsync(int id);
        Task<bool> MarkAllNotificationsAsReadAsync(string userId);

        // --- CAROUSEL IMAGES ---
        Task<List<CarouselImageViewModel>> GetCarouselImagesAsync();
        Task<bool> CreateCarouselImageAsync(CarouselImageViewModel input);
        Task<bool> DeleteCarouselImageAsync(int id);
    }
}
