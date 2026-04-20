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
    }
}
