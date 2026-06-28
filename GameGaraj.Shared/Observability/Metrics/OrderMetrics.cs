using System.Diagnostics.Metrics;

namespace GameGaraj.Shared.Observability.Metrics
{
    /// <summary>
    /// Business metrics for Order Service.
    /// Tracks order creation, cancellation, and processing duration.
    /// </summary>
    public sealed class OrderMetrics
    {
        private readonly Counter<long> _ordersCreated;
        private readonly Counter<long> _ordersCancelled;
        private readonly Counter<long> _ordersCompleted;
        private readonly Histogram<double> _orderProcessingDuration;

        public OrderMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("GameGaraj.Order");

            _ordersCreated = meter.CreateCounter<long>(
                "orders.created.total", "orders", "Total orders created");

            _ordersCancelled = meter.CreateCounter<long>(
                "orders.cancelled.total", "orders", "Total orders cancelled");

            _ordersCompleted = meter.CreateCounter<long>(
                "orders.completed.total", "orders", "Total orders completed successfully");

            _orderProcessingDuration = meter.CreateHistogram<double>(
                "orders.processing.duration", "ms", "Order processing duration in milliseconds");
        }

        public void OrderCreated(string? userId = null)
        {
            var tags = userId != null
                ? new KeyValuePair<string, object?>("user.id", userId)
                : default;
            _ordersCreated.Add(1, tags);
        }

        public void OrderCancelled(string? reason = null)
        {
            var tags = reason != null
                ? new KeyValuePair<string, object?>("cancellation.reason", reason)
                : default;
            _ordersCancelled.Add(1, tags);
        }

        public void OrderCompleted() => _ordersCompleted.Add(1);

        public TrackedDuration TrackProcessing() => new(_orderProcessingDuration);
    }
}
