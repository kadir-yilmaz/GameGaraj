using GameGaraj.Campaign.API.Models;

namespace GameGaraj.Campaign.API.Services.Abstract
{
    /// <summary>
    /// Kampanya kurallarının CRUD operasyonları için servis arayüzü.
    /// </summary>
    public interface ICampaignRuleService
    {
        Task<List<CampaignRule>> GetAllAsync();
        Task<List<CampaignRule>> GetActiveAsync();
        Task<CampaignRule?> GetByIdAsync(int id);
        Task<bool> SaveAsync(CampaignRule rule);
        Task<bool> UpdateAsync(CampaignRule rule);
        Task<bool> DeleteAsync(int id);
    }
}
