using MassTransit;
using GameGaraj.Shared.Events;
using GameGaraj.Campaign.API.Services.Abstract;

namespace GameGaraj.Campaign.API.Consumers
{
    public class CouponRewardTriggeredConsumer : IConsumer<CouponRewardTriggered>
    {
        private readonly ICouponRewardService _couponRewardService;
        private readonly ILogger<CouponRewardTriggeredConsumer> _logger;

        public CouponRewardTriggeredConsumer(
            ICouponRewardService couponRewardService,
            ILogger<CouponRewardTriggeredConsumer> logger)
        {
            _couponRewardService = couponRewardService;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<CouponRewardTriggered> context)
        {
            var message = context.Message;
            _logger.LogInformation($"[CouponRewardTriggeredConsumer] Received CouponRewardTriggered event. OrderId: {message.OrderId}, UserId: {message.UserId}, Amount: {message.Amount}");

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
