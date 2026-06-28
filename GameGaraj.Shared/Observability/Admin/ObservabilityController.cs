using GameGaraj.Shared.Logging;
using Microsoft.AspNetCore.Mvc;
using Serilog.Events;

namespace GameGaraj.Shared.Observability.Admin
{
    /// <summary>
    /// Admin API for managing observability settings at runtime.
    /// Mount this controller in each microservice for per-service control.
    /// 
    /// Endpoints:
    ///   GET  /api/observability/status          — Current service observability status
    ///   GET  /api/observability/log-level        — Current log level
    ///   PUT  /api/observability/log-level        — Change log level (with optional auto-revert)
    ///   GET  /api/observability/trace-sampling    — Current trace sampling ratio
    ///   PUT  /api/observability/trace-sampling    — Change trace sampling (with optional auto-revert)
    ///   GET  /api/observability/audit            — Audit log of changes
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ObservabilityController : ControllerBase
    {
        private readonly string _serviceName;
        private readonly TraceSamplingManager _samplingManager;
        private readonly ObservabilityAuditLog _auditLog;

        public ObservabilityController(
            TraceSamplingManager samplingManager,
            ObservabilityAuditLog auditLog)
        {
            _samplingManager = samplingManager;
            _auditLog = auditLog;

            // Determine service name from environment or configuration
            _serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME")
                           ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name
                           ?? "Unknown";
        }

        /// <summary>
        /// Returns current observability status for this service.
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                ServiceName = _serviceName,
                MachineName = Environment.MachineName,
                Timestamp = DateTime.UtcNow,
                LogLevel = LogLevelManager.GetLevel(_serviceName).ToString(),
                TraceSampling = new
                {
                    CurrentRatio = _samplingManager.CurrentRatio,
                    BaselineRatio = _samplingManager.BaselineRatio
                }
            });
        }

        /// <summary>
        /// Gets the current log level for this service.
        /// </summary>
        [HttpGet("log-level")]
        public IActionResult GetLogLevel()
        {
            var allLevels = LogLevelManager.GetAllLevels();
            return Ok(new
            {
                ServiceName = _serviceName,
                CurrentLevel = LogLevelManager.GetLevel(_serviceName).ToString(),
                AllRegisteredServices = allLevels.ToDictionary(
                    x => x.Key, 
                    x => x.Value.ToString())
            });
        }

        /// <summary>
        /// Changes the log level at runtime. Optionally reverts after a duration.
        /// </summary>
        [HttpPut("log-level")]
        public IActionResult SetLogLevel([FromBody] SetLogLevelRequest request)
        {
            if (!Enum.TryParse<LogEventLevel>(request.Level, true, out var newLevel))
            {
                return BadRequest(new
                {
                    Error = $"Invalid log level: {request.Level}",
                    ValidLevels = Enum.GetNames<LogEventLevel>()
                });
            }

            var targetService = request.ServiceName ?? _serviceName;
            var oldLevel = LogLevelManager.GetLevel(targetService);

            LogLevelManager.SetLevel(targetService, newLevel);

            // If duration specified, schedule auto-revert
            if (request.DurationMinutes > 0)
            {
                _ = Task.Delay(TimeSpan.FromMinutes(request.DurationMinutes)).ContinueWith(_ =>
                {
                    LogLevelManager.SetLevel(targetService, oldLevel);
                    _auditLog.Add(new ObservabilityAuditEntry
                    {
                        ChangedBy = "System (auto-revert)",
                        ServiceName = targetService,
                        ChangeType = "LogLevel",
                        OldValue = newLevel.ToString(),
                        NewValue = oldLevel.ToString(),
                        Reason = $"Auto-reverted after {request.DurationMinutes} minutes"
                    });
                });
            }

            _auditLog.Add(new ObservabilityAuditEntry
            {
                ChangedBy = request.ChangedBy ?? User.Identity?.Name ?? "Anonymous",
                ServiceName = targetService,
                ChangeType = "LogLevel",
                OldValue = oldLevel.ToString(),
                NewValue = newLevel.ToString(),
                Reason = request.Reason
            });

            return Ok(new
            {
                ServiceName = targetService,
                OldLevel = oldLevel.ToString(),
                NewLevel = newLevel.ToString(),
                AutoRevertMinutes = request.DurationMinutes > 0 ? request.DurationMinutes : (int?)null
            });
        }

        /// <summary>
        /// Gets the current trace sampling ratio.
        /// </summary>
        [HttpGet("trace-sampling")]
        public IActionResult GetTraceSampling()
        {
            return Ok(new
            {
                ServiceName = _serviceName,
                CurrentRatio = _samplingManager.CurrentRatio,
                CurrentPercent = $"{_samplingManager.CurrentRatio * 100:F1}%",
                BaselineRatio = _samplingManager.BaselineRatio,
                BaselinePercent = $"{_samplingManager.BaselineRatio * 100:F1}%"
            });
        }

        /// <summary>
        /// Changes the trace sampling ratio. Optionally reverts after a duration.
        /// </summary>
        [HttpPut("trace-sampling")]
        public IActionResult SetTraceSampling([FromBody] SetTraceSamplingRequest request)
        {
            if (request.Ratio < 0 || request.Ratio > 1)
            {
                return BadRequest(new { Error = "Ratio must be between 0.0 and 1.0" });
            }

            var oldRatio = _samplingManager.CurrentRatio;
            var duration = request.DurationMinutes > 0
                ? TimeSpan.FromMinutes(request.DurationMinutes)
                : (TimeSpan?)null;

            _samplingManager.SetSamplingRatio(request.Ratio, duration);

            _auditLog.Add(new ObservabilityAuditEntry
            {
                ChangedBy = request.ChangedBy ?? User.Identity?.Name ?? "Anonymous",
                ServiceName = _serviceName,
                ChangeType = "TraceSampling",
                OldValue = $"{oldRatio * 100:F1}%",
                NewValue = $"{request.Ratio * 100:F1}%",
                Reason = request.Reason
            });

            return Ok(new
            {
                ServiceName = _serviceName,
                OldRatio = oldRatio,
                NewRatio = request.Ratio,
                AutoRevertMinutes = request.DurationMinutes > 0 ? request.DurationMinutes : (int?)null
            });
        }

        /// <summary>
        /// Returns the audit log of observability changes.
        /// </summary>
        [HttpGet("audit")]
        public IActionResult GetAuditLog([FromQuery] int limit = 50)
        {
            return Ok(_auditLog.GetEntries(limit));
        }
    }

    // ── Request DTOs ──

    public sealed class SetLogLevelRequest
    {
        /// <summary>Log level: Verbose, Debug, Information, Warning, Error, Fatal</summary>
        public required string Level { get; init; }

        /// <summary>Target service name. If null, applies to the current service.</summary>
        public string? ServiceName { get; init; }

        /// <summary>Auto-revert after N minutes. 0 = permanent.</summary>
        public int DurationMinutes { get; init; }

        /// <summary>Who is making the change</summary>
        public string? ChangedBy { get; init; }

        /// <summary>Reason for the change</summary>
        public string? Reason { get; init; }
    }

    public sealed class SetTraceSamplingRequest
    {
        /// <summary>Sampling ratio (0.0 to 1.0). 1.0 = 100%</summary>
        public double Ratio { get; init; }

        /// <summary>Auto-revert after N minutes. 0 = permanent.</summary>
        public int DurationMinutes { get; init; }

        /// <summary>Who is making the change</summary>
        public string? ChangedBy { get; init; }

        /// <summary>Reason for the change</summary>
        public string? Reason { get; init; }
    }
}
