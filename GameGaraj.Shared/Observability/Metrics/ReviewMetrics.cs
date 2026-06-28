using System.Diagnostics.Metrics;

namespace GameGaraj.Shared.Observability.Metrics
{
    /// <summary>
    /// Business metrics for Review Service.
    /// Tracks review submissions, moderation actions, and content analysis.
    /// </summary>
    public sealed class ReviewMetrics
    {
        private readonly Counter<long> _reviewsSubmitted;
        private readonly Counter<long> _reviewsApproved;
        private readonly Counter<long> _reviewsRejected;
        private readonly Counter<long> _reviewsModerated;

        public ReviewMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("GameGaraj.Review");

            _reviewsSubmitted = meter.CreateCounter<long>(
                "reviews.submitted.total", "reviews", "Total reviews submitted");

            _reviewsApproved = meter.CreateCounter<long>(
                "reviews.approved.total", "reviews", "Total reviews approved");

            _reviewsRejected = meter.CreateCounter<long>(
                "reviews.rejected.total", "reviews", "Total reviews rejected by moderation");

            _reviewsModerated = meter.CreateCounter<long>(
                "reviews.moderated.total", "reviews", "Total reviews processed by moderation");
        }

        public void ReviewSubmitted() => _reviewsSubmitted.Add(1);
        public void ReviewApproved() => _reviewsApproved.Add(1);
        public void ReviewRejected() => _reviewsRejected.Add(1);
        public void ReviewModerated() => _reviewsModerated.Add(1);
    }
}
