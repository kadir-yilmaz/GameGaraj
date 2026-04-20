using GameGaraj.Campaign.API.Models;

namespace GameGaraj.Campaign.API.Rules
{
    public interface ICampaignRule
    {
        string RuleType { get; }
        CalculateDiscountResponse? Calculate(CalculateDiscountRequest request, CampaignRule rule);
    }
}
