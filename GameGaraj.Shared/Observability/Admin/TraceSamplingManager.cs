using OpenTelemetry.Trace;

namespace GameGaraj.Shared.Observability.Admin
{
    /// <summary>
    /// Manages trace sampling ratio at runtime.
    /// Allows temporarily increasing sampling for debugging, 
    /// then auto-reverting to the baseline ratio.
    /// </summary>
    public sealed class TraceSamplingManager : IDisposable
    {
        private double _samplingRatio;
        private readonly double _baselineRatio;
        private Timer? _revertTimer;
        private readonly object _lock = new();

        /// <summary>
        /// Creates a new TraceSamplingManager.
        /// </summary>
        /// <param name="baselineRatio">Default sampling ratio (0.0 to 1.0). 1.0 = 100%</param>
        public TraceSamplingManager(double baselineRatio = 1.0)
        {
            _baselineRatio = Math.Clamp(baselineRatio, 0.0, 1.0);
            _samplingRatio = _baselineRatio;
        }

        /// <summary>
        /// Gets the current sampling ratio.
        /// </summary>
        public double CurrentRatio
        {
            get { lock (_lock) { return _samplingRatio; } }
        }

        /// <summary>
        /// Gets the baseline (default) sampling ratio.
        /// </summary>
        public double BaselineRatio => _baselineRatio;

        /// <summary>
        /// Sets a temporary sampling ratio that reverts to baseline after the specified duration.
        /// </summary>
        /// <param name="ratio">Temporary ratio (0.0 to 1.0)</param>
        /// <param name="duration">How long to maintain the temporary ratio. Null = permanent.</param>
        public void SetSamplingRatio(double ratio, TimeSpan? duration = null)
        {
            lock (_lock)
            {
                _samplingRatio = Math.Clamp(ratio, 0.0, 1.0);

                _revertTimer?.Dispose();
                _revertTimer = null;

                if (duration.HasValue)
                {
                    _revertTimer = new Timer(
                        _ =>
                        {
                            lock (_lock)
                            {
                                _samplingRatio = _baselineRatio;
                                _revertTimer?.Dispose();
                                _revertTimer = null;
                            }
                        },
                        null,
                        duration.Value,
                        Timeout.InfiniteTimeSpan);
                }
            }
        }

        /// <summary>
        /// Resets to baseline ratio immediately.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _samplingRatio = _baselineRatio;
                _revertTimer?.Dispose();
                _revertTimer = null;
            }
        }

        public void Dispose()
        {
            _revertTimer?.Dispose();
        }
    }
}
