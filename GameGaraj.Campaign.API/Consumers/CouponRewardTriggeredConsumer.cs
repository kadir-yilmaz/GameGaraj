using MassTransit;
using GameGaraj.Shared.Events;
using GameGaraj.Campaign.API.Services.Abstract;
using GameGaraj.Shared.Chaos;

namespace GameGaraj.Campaign.API.Consumers
{
    public class CouponRewardTriggeredConsumer : IConsumer<CouponRewardTriggered>
    {
        private readonly ICouponRewardService _couponRewardService;
        private readonly IChaosManager _chaosManager;
        private readonly ILogger<CouponRewardTriggeredConsumer> _logger;

        public CouponRewardTriggeredConsumer(
            ICouponRewardService couponRewardService,
            IChaosManager chaosManager,
            ILogger<CouponRewardTriggeredConsumer> logger)
        {
            _couponRewardService = couponRewardService;
            _chaosManager = chaosManager;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<CouponRewardTriggered> context)
        {
            var message = context.Message;
            _logger.LogInformation($"[CouponRewardTriggeredConsumer] Received CouponRewardTriggered event. OrderId: {message.OrderId}, UserId: {message.UserId}, Amount: {message.Amount}");

            // 🛑 Chaos / Uyku & Gecikme Kontrolü
            try
            {
                var chaosRule = await _chaosManager.GetRuleAsync("campaign");
                if (chaosRule != null && chaosRule.Enabled)
                {
                    if (chaosRule.AlwaysFail)
                    {
                        _logger.LogWarning($"[Chaos] 💤 Campaign.API UYKU MODUNDA! Sipariş #{message.OrderId} için kupon ödülü bekletiliyor (Kuyrukta yeniden denenecek)...");
                        await Task.Delay(3000, context.CancellationToken);
                        throw new InvalidOperationException("[Chaos] Campaign.API uykuda (503 Service Unavailable). Kupon işlemi bekletiliyor.");
                    }

                    if (chaosRule.LatencyMs > 0)
                    {
                        _logger.LogInformation($"[Chaos] ⏱️ Campaign.API {chaosRule.LatencyMs} ms gecikme uygulanıyor...");
                        await Task.Delay(chaosRule.LatencyMs, context.CancellationToken);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[Chaos] Warning checking chaos rule: {ex.Message}");
            }

            try
            {
                // 1. Alışveriş kaydını ekle
                await _couponRewardService.AddPurchaseLogAsync(message.UserId, message.OrderId, message.Amount);
                _logger.LogInformation($"[CouponRewardTriggeredConsumer] Logged purchase for OrderId: {message.OrderId}, User: {message.UserId}");

                // 2. Kural kontrolü yap ve kuponları hediye et
                var grantedCoupons = await _couponRewardService.CheckAndGrantRewardsAsync(message.UserId);
                if (grantedCoupons.Any())
                {
                    _logger.LogInformation($"[CouponRewardTriggeredConsumer] User {message.UserId} earned {grantedCoupons.Count} new coupons!");
                    foreach (var coupon in grantedCoupons)
                    {
                        _logger.LogInformation($"   - Coupon: {coupon.Code} ({coupon.CouponType}: {coupon.Amount ?? coupon.Rate ?? 0})");
                    }
                }
                else
                {
                    _logger.LogInformation($"[CouponRewardTriggeredConsumer] User {message.UserId} did not qualify for any new coupon rewards.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[CouponRewardTriggeredConsumer] Error while processing coupon rewards for order: {message.OrderId}");
            }
        }
    }
}
