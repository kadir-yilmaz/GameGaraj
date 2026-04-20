using GameGaraj.Campaign.API.Models;

namespace GameGaraj.Campaign.API.Services.Abstract
{
    /// <summary>
    /// İndirim hesaplama motoru arayüzü.
    /// Sepet bilgisi alıp en avantajlı indirimi hesaplar.
    /// </summary>
    public interface ICampaignCalculationService
    {
        Task<CalculateDiscountResponse> CalculateAsync(CalculateDiscountRequest request);
    }
}
