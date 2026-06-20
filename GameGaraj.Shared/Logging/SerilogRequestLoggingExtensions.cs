using Microsoft.AspNetCore.Builder;
using Serilog;
using System.Security.Claims;
using Prometheus;

namespace GameGaraj.Shared.Logging
{
    public static class SerilogRequestLoggingExtensions
    {
        public static void UseCustomRequestLogging(this WebApplication app)
        {
            // Register Prometheus HTTP metrics middleware
            app.UseHttpMetrics();

            app.UseSerilogRequestLogging(options =>
            {
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

            // Expose the /metrics endpoint
            app.MapMetrics();
        }
    }
}
