using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace GameGaraj.Shared.Observability
{
    public static class PrometheusMetricsExtensions
    {
        public static WebApplicationBuilder AddPrometheusMetrics(
            this WebApplicationBuilder builder,
            string serviceName)
        {
            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics
                        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddPrometheusExporter();
                });

            return builder;
        }

        public static WebApplication UsePrometheusMetrics(this WebApplication app)
        {
            app.MapPrometheusScrapingEndpoint();
            return app;
        }
    }
}
