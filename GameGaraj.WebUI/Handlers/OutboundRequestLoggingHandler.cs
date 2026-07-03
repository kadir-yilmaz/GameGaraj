using System.Diagnostics;

namespace GameGaraj.WebUI.Handlers
{
    public class OutboundRequestLoggingHandler : DelegatingHandler
    {
        private readonly ILogger<OutboundRequestLoggingHandler> _logger;

        public OutboundRequestLoggingHandler(ILogger<OutboundRequestLoggingHandler> logger)
        {
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var targetService = ResolveTargetService(request.RequestUri);
            var requestPath = request.RequestUri?.AbsolutePath ?? string.Empty;

            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["LogType"] = "ExternalDependency",
                ["RequestArea"] = "WebUI",
                ["DependencyType"] = "HTTP",
                ["TargetService"] = targetService,
                ["RequestMethod"] = request.Method.Method,
                ["RequestPath"] = requestPath,
                ["TraceId"] = Activity.Current?.TraceId.ToString(),
                ["SpanId"] = Activity.Current?.SpanId.ToString()
            });

            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                stopwatch.Stop();

                var statusCode = (int)response.StatusCode;
                var level = statusCode >= 500
                    ? LogLevel.Warning
                    : statusCode >= 400
                        ? LogLevel.Warning
                        : LogLevel.Information;

                _logger.Log(
                    level,
                    "WebUI outbound HTTP request completed. Event={Event}, Method={Method}, Path={Path}, TargetService={TargetService}, StatusCode={StatusCode}, DurationMs={DurationMs}",
                    "OutboundRequestCompleted",
                    request.Method.Method,
                    requestPath,
                    targetService,
                    statusCode,
                    stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(
                    ex,
                    "WebUI outbound HTTP request failed. Event={Event}, Method={Method}, Path={Path}, TargetService={TargetService}, DurationMs={DurationMs}, Reason={Reason}",
                    "OutboundRequestFailed",
                    request.Method.Method,
                    requestPath,
                    targetService,
                    stopwatch.ElapsedMilliseconds,
                    ex.GetType().Name);

                throw;
            }
        }

        private static string ResolveTargetService(Uri? requestUri)
        {
            if (requestUri == null)
            {
                return "Unknown";
            }

            var path = requestUri.AbsolutePath;
            if (path.StartsWith("/api/catalog", StringComparison.OrdinalIgnoreCase))
            {
                return "Catalog API";
            }

            if (path.StartsWith("/api/basket", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/api/favorites", StringComparison.OrdinalIgnoreCase))
            {
                return "Basket API";
            }

            if (path.StartsWith("/api/order", StringComparison.OrdinalIgnoreCase))
            {
                return "Order API";
            }

            if (path.StartsWith("/api/payment", StringComparison.OrdinalIgnoreCase))
            {
                return "Payment API";
            }

            if (path.StartsWith("/api/review", StringComparison.OrdinalIgnoreCase))
            {
                return "Review API";
            }

            if (path.StartsWith("/api/campaign", StringComparison.OrdinalIgnoreCase))
            {
                return "Campaign API";
            }

            if (path.StartsWith("/api/photostock", StringComparison.OrdinalIgnoreCase))
            {
                return "PhotoStock API";
            }

            if (requestUri.Host.Contains("keycloak", StringComparison.OrdinalIgnoreCase)
                || path.Contains("/realms/", StringComparison.OrdinalIgnoreCase))
            {
                return "Identity Provider";
            }

            return requestUri.Host;
        }
    }
}
