using System.Diagnostics.Metrics;

namespace GameGaraj.Shared.Observability.Metrics
{
    /// <summary>
    /// Business metrics for API Gateway.
    /// Tracks routing decisions, upstream errors, and rate limiting.
    /// </summary>
    public sealed class GatewayMetrics
    {
        private readonly Counter<long> _requestsRouted;
        private readonly Counter<long> _upstreamErrors;
        private readonly Counter<long> _authFailures;

        public GatewayMetrics(IMeterFactory meterFactory)
        {
            var meter = meterFactory.Create("GameGaraj.Gateway");

            _requestsRouted = meter.CreateCounter<long>(
                "gateway.requests.routed.total", null, "Total requests routed to backend services");

            _upstreamErrors = meter.CreateCounter<long>(
                "gateway.upstream.errors.total", null, "Total upstream service errors");

            _authFailures = meter.CreateCounter<long>(
                "gateway.auth.failures.total", null, "Total authentication failures at gateway");
        }

        public void RequestRouted(string? cluster = null)
        {
            var tags = cluster != null
                ? new KeyValuePair<string, object?>("upstream.cluster", cluster)
                : default;
            _requestsRouted.Add(1, tags);
        }

        public void UpstreamError(string? cluster = null)
        {
            var tags = cluster != null
                ? new KeyValuePair<string, object?>("upstream.cluster", cluster)
                : default;
            _upstreamErrors.Add(1, tags);
        }

        public void AuthFailure() => _authFailures.Add(1);
    }
}
