using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using GameGaraj.Shared.Observability.Admin;

namespace GameGaraj.Shared.Observability
{
    /// <summary>
    /// Central OpenTelemetry configuration for all GameGaraj microservices.
    /// Provides unified tracing (OTLP → Jaeger) and metrics (Prometheus exporter).
    /// </summary>
    public static class OpenTelemetryConfiguration
    {
        /// <summary>
        /// Adds OpenTelemetry tracing and metrics to the application.
        /// Call this in Program.cs after AddSerilogLogging().
        /// </summary>
        public static WebApplicationBuilder AddObservability(
            this WebApplicationBuilder builder,
            string serviceName,
            string serviceVersion = "1.0.0")
        {
            var environment = builder.Environment.EnvironmentName;
            var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
            var defaultSamplingRatio = environment == "Development" ? 1.0 : 0.05;
            var samplingRatio = builder.Configuration.GetValue<double?>("OpenTelemetry:SamplingRatio")
                                ?? defaultSamplingRatio;
            samplingRatio = Math.Clamp(samplingRatio, 0.0, 1.0);

            // Resource — shared identity for all telemetry signals
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

            // ── Sampler Configuration ──
            // Giriş noktaları (WebUI, Gateway) gelen isteklere göre trace başlatabilir.
            // Diğer iç mikroservisler (Catalog, Order, Payment vb.) ise ASLA kendi başlarına sıfırdan 
            // yeni bir kök (root) trace başlatmazlar (0.0 oranı). Sadece üst servisten gelen trace'i devam ettirirler (ParentBased).
            var isEntryPoint = serviceName == "GameGaraj.WebUI" || serviceName == "GameGaraj.Gateway";
            var activeRatio = isEntryPoint ? samplingRatio : 0.0;
            var sampler = new ParentBasedSampler(new TraceIdRatioBasedSampler(activeRatio));

            builder.Services.AddOpenTelemetry()
                // ── Tracing ──
                .WithTracing(tracing =>
                {
                    tracing
                        .SetResourceBuilder(resourceBuilder)
                        .SetSampler(sampler)
                        .AddAspNetCoreInstrumentation(opts =>
                        {
                            opts.RecordException = true;
                            opts.Filter = ctx =>
                            {
                                var path = ctx.Request.Path.Value ?? "";
                                if (path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase) ||
                                    path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
                                    path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
                                {
                                    return false;
                                }

                                // WebUI için sadece sipariş ve ödeme akışını izle (Gürültüyü önlemek için)
                                if (serviceName == "GameGaraj.WebUI")
                                {
                                    return path.StartsWith("/Order", StringComparison.OrdinalIgnoreCase);
                                }

                                // Gateway için sadece sipariş ve ödeme API isteklerini izle
                                if (serviceName == "GameGaraj.Gateway")
                                {
                                    return path.StartsWith("/api/orders", StringComparison.OrdinalIgnoreCase) ||
                                           path.StartsWith("/api/payments", StringComparison.OrdinalIgnoreCase) ||
                                           path.StartsWith("/api/order", StringComparison.OrdinalIgnoreCase) ||
                                           path.StartsWith("/api/payment", StringComparison.OrdinalIgnoreCase);
                                }

                                return true;
                            };
                        })
                        .AddHttpClientInstrumentation(opts =>
                        {
                            opts.RecordException = true;
                        })
                        .AddEntityFrameworkCoreInstrumentation(opts =>
                        {
                            opts.SetDbStatementForText = true;
                        })
                        .AddSqlClientInstrumentation(opts =>
                        {
                            opts.SetDbStatementForText = true;
                            opts.RecordException = true;
                        })
                        .AddSource(serviceName)
                        .AddSource($"{serviceName}.*")
                        .AddSource("MassTransit");

                    // OTLP exporter (Jaeger) — only if endpoint is configured
                    if (!string.IsNullOrEmpty(otlpEndpoint) && otlpEndpoint != "disabled")
                    {
                        tracing.AddOtlpExporter(opts =>
                        {
                            opts.Endpoint = new Uri(otlpEndpoint);
                            opts.Protocol = OtlpExportProtocol.Grpc;
                        });
                    }
                })
                // ── Metrics ──
                .WithMetrics(metrics =>
                {
                    metrics
                        .SetResourceBuilder(resourceBuilder)
                        .SetExemplarFilter(ExemplarFilterType.TraceBased)
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddProcessInstrumentation()
                        .AddMeter(serviceName)
                        // Explicit registration of ALL business metric meters.
                        // Wildcard ("GameGaraj.*") is NOT reliable in OTel .NET SDK.
                        .AddMeter("GameGaraj.Basket")
                        .AddMeter("GameGaraj.Order")
                        .AddMeter("GameGaraj.Payment")
                        .AddMeter("GameGaraj.Campaign")
                        .AddMeter("GameGaraj.Review")
                        .AddMeter("GameGaraj.Invoice")
                        .AddMeter("GameGaraj.PhotoStock")
                        .AddMeter("GameGaraj.Gateway")
                        .AddMeter("GameGaraj.Catalog")
                        .AddMeter("GameGaraj.WebUI")
                        .AddPrometheusExporter();
                });

            // ── Admin Observability Services ──
            builder.Services.AddSingleton(new TraceSamplingManager(
                baselineRatio: samplingRatio));
            builder.Services.AddSingleton<ObservabilityAuditLog>();

            // Set SERVICE_NAME so the admin controller can identify itself
            Environment.SetEnvironmentVariable("SERVICE_NAME", serviceName);

            return builder;
        }

        /// <summary>
        /// Maps the Prometheus scraping endpoint at /metrics.
        /// Call this in Program.cs after building the app.
        /// </summary>
        public static WebApplication UseObservability(this WebApplication app)
        {
            app.MapPrometheusScrapingEndpoint();
            return app;
        }
    }
}
