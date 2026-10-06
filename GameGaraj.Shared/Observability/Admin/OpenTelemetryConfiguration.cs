using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using GameGaraj.Shared.Observability.Admin;

namespace GameGaraj.Shared.Observability.Admin
{
    /// <summary>
    /// Central OpenTelemetry configuration for all GameGaraj microservices.
    /// Provides unified Logging, Tracing, and Metrics via OTLP.
    /// </summary>
    public static class OpenTelemetryConfiguration
    {
        public static WebApplicationBuilder AddObservability(
            this WebApplicationBuilder builder,
            string serviceName,
            string serviceVersion = "1.0.0")
        {
            var environment = builder.Environment.EnvironmentName;
            var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];

            // ── Resource ── (Ortak kimlik)
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environment,
                    ["host.name"] = Environment.MachineName
                });

            // ── 1. Logging ──
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(); // Lokal geliştirme için konsol logu

            if (!string.IsNullOrEmpty(otlpEndpoint) && otlpEndpoint != "disabled")
            {
                builder.Logging.AddOpenTelemetry(logging =>
                {
                    logging.IncludeFormattedMessage = true;
                    logging.IncludeScopes = true;
                    logging.SetResourceBuilder(resourceBuilder);
                    logging.AddOtlpExporter(opts => 
                    {
                        opts.Endpoint = new Uri(otlpEndpoint);
                        opts.Protocol = OtlpExportProtocol.Grpc;
                    });
                });
            }

            // ── 2. Tracing & Metrics ──
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing
                        .SetResourceBuilder(resourceBuilder)
                        .AddAspNetCoreInstrumentation(opts => opts.RecordException = true)
                        .AddHttpClientInstrumentation(opts => opts.RecordException = true)
                        .AddSource(serviceName)
                        .AddSource($"{serviceName}.*")
                        .AddSource("MassTransit");

                    if (!string.IsNullOrEmpty(otlpEndpoint) && otlpEndpoint != "disabled")
                    {
                        tracing.AddOtlpExporter(opts =>
                        {
                            opts.Endpoint = new Uri(otlpEndpoint);
                            opts.Protocol = OtlpExportProtocol.Grpc;
                        });
                    }
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .SetResourceBuilder(resourceBuilder)
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddProcessInstrumentation()
                        .AddPrometheusExporter();
                });

            // ── Admin Services ──
            // Sadece ILogger kullanarak bağımlılıkları azalttık.
            Environment.SetEnvironmentVariable("SERVICE_NAME", serviceName);

            return builder;
        }

        public static WebApplication UseObservability(this WebApplication app)
        {
            app.MapPrometheusScrapingEndpoint();
            return app;
        }
    }
}
