using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace GameGaraj.Shared.Observability.Metrics
{
    /// <summary>
    /// Helper for measuring operation durations with Histogram instruments.
    /// Usage: using (metrics.TrackProcessing()) { ... }
    /// </summary>
    public sealed class TrackedDuration : IDisposable
    {
        private readonly Histogram<double> _histogram;
        private readonly Stopwatch _stopwatch;
        private readonly KeyValuePair<string, object?>[] _tags;

        public TrackedDuration(Histogram<double> histogram, params KeyValuePair<string, object?>[] tags)
        {
            _histogram = histogram;
            _tags = tags;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _histogram.Record(_stopwatch.Elapsed.TotalMilliseconds, _tags);
        }
    }
}
