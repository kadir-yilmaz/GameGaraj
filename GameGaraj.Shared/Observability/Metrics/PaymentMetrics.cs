using System.Diagnostics.Metrics;

namespace GameGaraj.Shared.Observability.Metrics
{
    /// <summary>
    /// Business metrics for Payment Service.
    /// Tracks payment attempts, successes, failures, and processing duration.
    /// </summary>
    public sealed class PaymentMetrics
    {
        private readonly Counter<long> _paymentsTotal;
        private readonly Counter<long> _paymentsSucceeded;
        private readonly Counter<long> _paymentsFailed;
        private readonly Histogram<double> _paymentDuration;

        public PaymentMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("GameGaraj.Payment");

            _paymentsTotal = meter.CreateCounter<long>(
                "payments.total", null, "Total payment attempts");

            _paymentsSucceeded = meter.CreateCounter<long>(
                "payments.succeeded.total", null, "Total successful payments");

            _paymentsFailed = meter.CreateCounter<long>(
                "payments.failed.total", null, "Total failed payments");

            _paymentDuration = meter.CreateHistogram<double>(
                "payments.duration", "ms", "Payment processing duration in milliseconds");
        }

        public void PaymentAttempted(string? provider = null)
        {
            var tags = provider != null
                ? new KeyValuePair<string, object?>("payment.provider", provider)
                : default;
            _paymentsTotal.Add(1, tags);
        }

        public void PaymentSucceeded(string? provider = null)
        {
            var tags = provider != null
                ? new KeyValuePair<string, object?>("payment.provider", provider)
                : default;
            _paymentsSucceeded.Add(1, tags);
        }

        public void PaymentFailed(string? reason = null)
        {
            var tags = reason != null
                ? new KeyValuePair<string, object?>("failure.reason", reason)
                : default;
            _paymentsFailed.Add(1, tags);
        }

        public TrackedDuration TrackPayment() => new(_paymentDuration);
    }
}
