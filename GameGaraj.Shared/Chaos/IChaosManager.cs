namespace GameGaraj.Shared.Chaos
{
    public interface IChaosManager
    {
        Task<ChaosRule> GetRuleAsync(string serviceName);
        Task<Dictionary<string, ChaosRule>> GetAllRulesAsync();
        Task SetRuleAsync(ChaosRule rule);
        Task ResetRuleAsync(string serviceName);
        Task ResetAllRulesAsync();
        Task<(bool shouldFail, int attempt, string reason)> EvaluateRequestAsync(string serviceName, string path);
    }
}
