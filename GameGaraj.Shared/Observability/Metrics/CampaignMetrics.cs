using System.Diagnostics.Metrics;

namespace GameGaraj.Shared.Observability.Metrics
{
    /// <summary>
    /// Business metrics for Campaign Service.
    /// Tracks coupon usage, campaign calculations, and reward distributions.
    /// </summary>
    public sealed class CampaignMetrics
    {
        private readonly Counter<long> _couponsUsed;
        private readonly Counter<long> _couponsCreated;
        private readonly Counter<long> _campaignCalculations;
        private readonly Counter<long> _rewardsDistributed;
        private readonly Counter<long> _notificationsSent;
        private readonly Counter<long> _notificationsFailed;

        public CampaignMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("GameGaraj.Campaign");

            _couponsUsed = meter.CreateCounter<long>(
                "campaign.coupons.used.total", "coupons", "Total coupons used");

            _couponsCreated = meter.CreateCounter<long>(
                "campaign.coupons.created.total", "coupons", "Total coupons created");

            _campaignCalculations = meter.CreateCounter<long>(
                "campaign.calculations.total", "calculations", "Total campaign calculations performed");

            _rewardsDistributed = meter.CreateCounter<long>(
                "campaign.rewards.distributed.total", "rewards", "Total rewards distributed");

            _notificationsSent = meter.CreateCounter<long>(
                "campaign.notifications.sent.total", "notifications", "Total notifications sent");

            _notificationsFailed = meter.CreateCounter<long>(
                "campaign.notifications.failed.total", "notifications", "Total notifications failed");
        }

        public void CouponUsed() => _couponsUsed.Add(1);
        public void CouponCreated() => _couponsCreated.Add(1);
        public void CampaignCalculated() => _campaignCalculations.Add(1);
        public void RewardDistributed() => _rewardsDistributed.Add(1);
        public void NotificationSent() => _notificationsSent.Add(1);
        public void NotificationFailed() => _notificationsFailed.Add(1);
    }
}
