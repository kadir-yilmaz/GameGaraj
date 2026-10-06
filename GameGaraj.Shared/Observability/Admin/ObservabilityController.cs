using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.Shared.Observability.Admin
{
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
            _serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME")
                           ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name
                           ?? "Unknown";
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                ServiceName = _serviceName,
                MachineName = Environment.MachineName,
                Timestamp = DateTime.UtcNow,
                TraceSampling = new
                {
                    CurrentRatio = _samplingManager.CurrentRatio,
                    BaselineRatio = _samplingManager.BaselineRatio
                }
            });
        }

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

        [HttpPut("trace-sampling")]
        public IActionResult SetTraceSampling([FromBody] SetTraceSamplingRequest request)
        {
            if (request.Ratio < 0 || request.Ratio > 1) return BadRequest(new { Error = "Ratio must be between 0.0 and 1.0" });
            var oldRatio = _samplingManager.CurrentRatio;
            var duration = request.DurationMinutes > 0 ? TimeSpan.FromMinutes(request.DurationMinutes) : (TimeSpan?)null;
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

        [HttpGet("audit")]
        public IActionResult GetAuditLog([FromQuery] int limit = 50) => Ok(_auditLog.GetEntries(limit));
    }

    public sealed class SetTraceSamplingRequest
    {
        public double Ratio { get; init; }
        public int DurationMinutes { get; init; }
        public string? ChangedBy { get; init; }
        public string? Reason { get; init; }
    }
}
