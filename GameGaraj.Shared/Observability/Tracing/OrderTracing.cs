using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GameGaraj.Shared.Observability
{
    /// <summary>
    /// Sadece sipariş akışını (Checkout → Order → Stock → Payment → Invoice) Jaeger'a gönderen sade tracing kurulumu.
    /// Kullanım: builder.AddOrderTracing("GameGaraj.Order");
    /// </summary>
    public static class OrderTracing
    {
        public const string SourceName = "GameGaraj.OrderFlow";
        public static readonly ActivitySource Source = new(SourceName);

        // Sipariş akışına ait HTTP yolları; diğer istekler (katalog gezme, health, metrics...) trace edilmez.
        private static readonly string[] OrderPaths = { "/order", "/payment", "/stock/validate" };
        private static readonly string[] IgnoredPaths = { "notification", "coupon", "/history", "/health", "/metrics", "/swagger" };

        public static WebApplicationBuilder AddOrderTracing(this WebApplicationBuilder builder, string serviceName)
        {
            var endpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
            if (string.IsNullOrWhiteSpace(endpoint) || endpoint == "disabled")
            {
                return builder;
            }

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(r => r.AddService(serviceName))
                .WithTracing(t => t
                    .AddSource(SourceName)      // AppDiagnostics ile açılan sipariş adımları
                    .AddSource("MassTransit")   // RabbitMQ publish/consume (servisler arası trace bağlantısı)
                    .AddAspNetCoreInstrumentation(o =>
                    {
                        o.RecordException = true;
                        o.Filter = ctx => IsOrderPath(ctx.Request.Path.Value);
                    })
                    .AddHttpClientInstrumentation(o =>
                    {
                        o.RecordException = true;
                        o.FilterHttpRequestMessage = req => IsOrderPath(req.RequestUri?.AbsolutePath);
                    })
                    .AddOtlpExporter(o => o.Endpoint = new Uri(endpoint)));

            return builder;
        }

        private static bool IsOrderPath(string? path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            return OrderPaths.Any(p => path.Contains(p, StringComparison.OrdinalIgnoreCase))
                && !IgnoredPaths.Any(p => path.Contains(p, StringComparison.OrdinalIgnoreCase));
        }
    }
}
