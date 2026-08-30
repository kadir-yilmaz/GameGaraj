using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GameGaraj.Shared.Chaos
{
    public class ChaosManager : IChaosManager
    {
        private readonly ILogger<ChaosManager> _logger;
        private readonly IConnectionMultiplexer? _redis;
        private static readonly string[] KnownServices = { "catalog", "order", "payment", "invoice", "campaign", "notification" };
        private const string KeyPrefix = "chaos:rule:";
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (ChaosRule Rule, DateTime ExpiresAt)> _ruleCache = new();

        public ChaosManager(IConfiguration configuration, ILogger<ChaosManager> logger)
        {
            _logger = logger;

            var redisUrl = configuration["Redis"] 
                           ?? configuration["RedisUrl"] 
                           ?? configuration["RedisSentinel:EndPoints:0"] 
                           ?? "localhost:6380";

            try
            {
                var options = ConfigurationOptions.Parse(redisUrl);
                options.AbortOnConnectFail = false;
                options.ConnectTimeout = 2000;
                options.SyncTimeout = 2000;
                _redis = ConnectionMultiplexer.Connect(options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ChaosManager] Could not connect to Redis at {RedisUrl}. Chaos features will remain disabled.", redisUrl);
            }
        }

        public async Task<ChaosRule> GetRuleAsync(string serviceName)
        {
            var cleanName = serviceName.ToLowerInvariant();

            if (_ruleCache.TryGetValue(cleanName, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            {
                return cached.Rule;
            }

            if (_redis == null || !_redis.IsConnected)
            {
                return new ChaosRule { ServiceName = cleanName, Enabled = false };
            }

            try
            {
                var db = _redis.GetDatabase();
                var json = await db.StringGetAsync($"{KeyPrefix}{cleanName}");
                if (json.IsNullOrEmpty)
                {
                    var defaultRule = new ChaosRule { ServiceName = cleanName, Enabled = false };
                    _ruleCache[cleanName] = (defaultRule, DateTime.UtcNow.AddMilliseconds(1000));
                    return defaultRule;
                }

                var rule = JsonSerializer.Deserialize<ChaosRule>(json!) ?? new ChaosRule { ServiceName = cleanName, Enabled = false };
                _ruleCache[cleanName] = (rule, DateTime.UtcNow.AddMilliseconds(1000));
                return rule;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[ChaosManager] Error getting rule for {ServiceName}", cleanName);
                return new ChaosRule { ServiceName = cleanName, Enabled = false };
            }
        }

        public async Task<Dictionary<string, ChaosRule>> GetAllRulesAsync()
        {
            var result = new Dictionary<string, ChaosRule>();
            foreach (var s in KnownServices)
            {
                result[s] = await GetRuleAsync(s);
            }
            return result;
        }

        public async Task SetRuleAsync(ChaosRule rule)
        {
            var cleanName = rule.ServiceName.ToLowerInvariant();
            rule.ServiceName = cleanName;
            _ruleCache[cleanName] = (rule, DateTime.UtcNow.AddMilliseconds(1000));

            if (_redis == null || !_redis.IsConnected) return;

            try
            {
                var db = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(rule);
                await db.StringSetAsync($"{KeyPrefix}{cleanName}", json);
                _logger.LogInformation("[ChaosManager] Chaos rule updated for {ServiceName}: Enabled={Enabled}, AlwaysFail={AlwaysFail}, FailUntilAttempt={FailUntilAttempt}, Latency={LatencyMs}",
                    cleanName, rule.Enabled, rule.AlwaysFail, rule.FailUntilAttempt, rule.LatencyMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChaosManager] Failed to set chaos rule for {ServiceName}", rule.ServiceName);
            }
        }

        public async Task ResetRuleAsync(string serviceName)
        {
            var cleanName = serviceName.ToLowerInvariant();
            _ruleCache.TryRemove(cleanName, out _);

            if (_redis == null || !_redis.IsConnected) return;

            try
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync($"{KeyPrefix}{cleanName}");
                _logger.LogInformation("[ChaosManager] Chaos rule reset for {ServiceName}", cleanName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChaosManager] Failed to reset rule for {ServiceName}", cleanName);
            }
        }

        public async Task ResetAllRulesAsync()
        {
            _ruleCache.Clear();
            foreach (var s in KnownServices)
            {
                await ResetRuleAsync(s);
            }
        }

        public async Task<(bool shouldFail, int attempt, string reason)> EvaluateRequestAsync(string serviceName, string path)
        {
            var rule = await GetRuleAsync(serviceName);
            if (!rule.Enabled)
            {
                return (false, 0, string.Empty);
            }

            // Apply synthetic latency if configured
            if (rule.LatencyMs > 0)
            {
                await Task.Delay(rule.LatencyMs);
            }

            // Block or AlwaysFail
            if (rule.BlockPipeline || rule.AlwaysFail)
            {
                return (true, 1, $"[Chaos] Service '{serviceName}' is blocked/failing (AlwaysFail = true)");
            }

            // Fail Until Attempt (Simulate transient failures for Retry / Polly)
            if (rule.FailUntilAttempt > 0)
            {
                int currentAttempt = 1;
                if (_redis != null && _redis.IsConnected)
                {
                    try
                    {
                        var db = _redis.GetDatabase();
                        var attemptKey = $"chaos:attempt:{serviceName.ToLowerInvariant()}";
                        currentAttempt = (int)await db.StringIncrementAsync(attemptKey);
                        // Auto-expire attempt counter after 2 minutes of inactivity
                        await db.KeyExpireAsync(attemptKey, TimeSpan.FromMinutes(2));

                        // Sync current attempt into rule
                        rule.CurrentAttempts = currentAttempt;
                        rule.LastTriggeredAt = DateTime.UtcNow;
                        await SetRuleAsync(rule);
                    }
                    catch
                    {
                        currentAttempt = 1;
                    }
                }

                if (currentAttempt < rule.FailUntilAttempt)
                {
                    return (true, currentAttempt, $"[Chaos] Transient failure simulation: Attempt {currentAttempt}/{rule.FailUntilAttempt} failed for '{serviceName}'");
                }
                else
                {
                    // Threshold reached! Allow request through and reset attempt counter
                    if (_redis != null && _redis.IsConnected)
                    {
                        try
                        {
                            var db = _redis.GetDatabase();
                            await db.KeyDeleteAsync($"chaos:attempt:{serviceName.ToLowerInvariant()}");
                        }
                        catch { }
                    }
                    return (false, currentAttempt, $"[Chaos] Succeeded on attempt {currentAttempt}/{rule.FailUntilAttempt} for '{serviceName}'");
                }
            }

            return (false, 0, string.Empty);
        }
    }
}
