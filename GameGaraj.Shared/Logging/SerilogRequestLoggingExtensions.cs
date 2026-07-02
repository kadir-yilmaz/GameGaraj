using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using System.Security.Claims;

namespace GameGaraj.Shared.Logging
{
    public static class SerilogRequestLoggingExtensions
    {
        public static void UseCustomRequestLogging(this WebApplication app, bool includeGatewayRouting = false)
        {
            app.UseSerilogRequestLogging(options =>
            {
                if (includeGatewayRouting)
                {
                    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} routed to {TargetService}. StatusCode={StatusCode} DurationMs={Elapsed:0.0000}";
                }

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
                        : httpContext.Response.StatusCode >= 400
                            ? LogEventLevel.Warning
                            : LogEventLevel.Information;
                };

                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    var user = httpContext.User;
                    string userIdentity = "Anonymous";
                    string? userId = TryGetHeader(httpContext, "X-User-Id");

                    if (user?.Identity?.IsAuthenticated == true)
                    {
                        userId = userId
                                 ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                 ?? user.FindFirst("sub")?.Value;

                        userIdentity = user.FindFirst(ClaimTypes.Email)?.Value 
                                       ?? user.FindFirst("email")?.Value
                                       ?? userId
                                       ?? "AuthenticatedUser";
                    }
                    else if (httpContext.Request.Headers.TryGetValue("X-User-Email", out var userEmailHeader) && !string.IsNullOrEmpty(userEmailHeader))
                    {
                        userIdentity = userEmailHeader.ToString();
                    }
                    else if (!string.IsNullOrEmpty(userId))
                    {
                        userIdentity = userId == "anonymous-user" ? GetGuestId(httpContext) ?? "Anonymous" : FormatUserIdentity(userId);
                    }
                    else
                    {
                        userId = GetGuestId(httpContext);
                        if (!string.IsNullOrEmpty(userId))
                        {
                            userIdentity = userId;
                        }
                    }

                    if (userId == "anonymous-user")
                    {
                        var guestId = GetGuestId(httpContext);
                        if (!string.IsNullOrEmpty(guestId))
                        {
                            userId = guestId;
                            userIdentity = guestId;
                        }
                    }

                    diagnosticContext.Set("LogType", "HttpRequest");
                    diagnosticContext.Set("TargetService", includeGatewayRouting ? ResolveGatewayTargetService(httpContext.Request.Path.Value) : null);
                    diagnosticContext.Set("UserIdentity", userIdentity);
                    diagnosticContext.Set("UserId", userId ?? "anonymous-user");
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                    diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
                    diagnosticContext.Set("TraceId", Activity.Current?.TraceId.ToString());
                    diagnosticContext.Set("SpanId", Activity.Current?.SpanId.ToString());
                };
            });
        }

        private static string? GetGuestId(HttpContext httpContext)
        {
            const string guestCookieName = "GameGarajGuestId";
            return httpContext.Request.Cookies.TryGetValue(guestCookieName, out var guestId) && !string.IsNullOrWhiteSpace(guestId)
                ? guestId
                : null;
        }

        private static string? TryGetHeader(HttpContext httpContext, string headerName)
        {
            return httpContext.Request.Headers.TryGetValue(headerName, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.ToString()
                : null;
        }

        private static string FormatUserIdentity(string userId)
        {
            return userId.StartsWith("guest-", StringComparison.OrdinalIgnoreCase)
                ? userId
                : $"User-{userId}";
        }

        private static string ResolveGatewayTargetService(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "Unknown";
            }

            if (path.StartsWith("/api/catalog", StringComparison.OrdinalIgnoreCase))
            {
                return "Catalog API";
            }

            if (path.StartsWith("/api/basket", StringComparison.OrdinalIgnoreCase))
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

            return "Unknown";
        }
    }
}
