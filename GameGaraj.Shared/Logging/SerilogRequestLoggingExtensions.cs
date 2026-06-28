using Microsoft.AspNetCore.Builder;
using Serilog;
using Serilog.Events;
using System.Security.Claims;

namespace GameGaraj.Shared.Logging
{
    public static class SerilogRequestLoggingExtensions
    {
        public static void UseCustomRequestLogging(this WebApplication app)
        {
            app.UseSerilogRequestLogging(options =>
            {
                // Filter out telemetry, health checks, and API docs to prevent Elasticsearch pollution
                options.GetLevel = (httpContext, elapsedMs, ex) =>
                {
                    if (ex != null) return LogEventLevel.Error;

                    var path = httpContext.Request.Path.Value;
                    if (path != null && (
                        path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)))
                    {
                        // Setting to Verbose ignores these logs under standard Information level
                        return LogEventLevel.Verbose;
                    }

                    return httpContext.Response.StatusCode >= 500 
                        ? LogEventLevel.Error 
                        : LogEventLevel.Information;
                };

                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    var user = httpContext.User;
                    string userIdentity = "Anonymous";

                    if (user?.Identity?.IsAuthenticated == true)
                    {
                        userIdentity = user.FindFirst(ClaimTypes.Email)?.Value 
                                       ?? user.FindFirst("email")?.Value
                                       ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                       ?? user.FindFirst("sub")?.Value 
                                       ?? "AuthenticatedUser";
                    }
                    else if (httpContext.Request.Headers.TryGetValue("X-User-Email", out var userEmailHeader) && !string.IsNullOrEmpty(userEmailHeader))
                    {
                        userIdentity = userEmailHeader;
                    }
                    else if (httpContext.Request.Headers.TryGetValue("X-User-Id", out var userIdHeader) && !string.IsNullOrEmpty(userIdHeader))
                    {
                        userIdentity = userIdHeader == "anonymous-user" ? "Anonymous" : $"User-{userIdHeader}";
                    }

                    diagnosticContext.Set("UserIdentity", userIdentity);
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                    diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
                };
            });
        }
    }
}
