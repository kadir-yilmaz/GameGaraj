using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameGaraj.Shared.Chaos
{
    public class ChaosMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _serviceName;
        private readonly ILogger<ChaosMiddleware> _logger;

        public ChaosMiddleware(RequestDelegate next, string serviceName, ILogger<ChaosMiddleware> logger)
        {
            _next = next;
            _serviceName = serviceName;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IChaosManager chaosManager)
        {
            // Skip health check, swagger, observability endpoints
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api/observability", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var (shouldFail, attempt, reason) = await chaosManager.EvaluateRequestAsync(_serviceName, path);

            if (shouldFail)
            {
                _logger.LogWarning("[ChaosMiddleware] 💥 Intercepted request for {ServiceName} ({Path}). Returning simulated failure: {Reason}",
                    _serviceName, path, reason);

                context.Response.Headers.TryAdd("X-Chaos-Simulated", "true");
                context.Response.Headers.TryAdd("X-Chaos-Attempt", attempt.ToString());
                context.Response.Headers.TryAdd("X-Chaos-Reason", reason);

                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    Success = false,
                    Message = reason,
                    IsChaosSimulated = true,
                    Attempt = attempt,
                    Service = _serviceName
                });
                return;
            }

            await _next(context);
        }
    }
}
