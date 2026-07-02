using GameGaraj.Gateway.Extensions;
using GameGaraj.Shared.Logging;
using GameGaraj.Shared.Observability;
using System.Diagnostics;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// Yüksek anlık yük testleri (Load Test) için ThreadPool'u baştan genişletiyoruz
System.Threading.ThreadPool.SetMinThreads(1000, 1000);

// Serilog Ekle
builder.AddSerilogLogging("Gateway");

// OpenTelemetry (Tracing + Metrics)
builder.AddObservability(ObservabilityConstants.GatewayService);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilderContext =>
    {
        transformBuilderContext.AddRequestTransform(transformContext =>
        {
            var activity = Activity.Current;
            if (activity != null && !string.IsNullOrWhiteSpace(activity.Id))
            {
                transformContext.ProxyRequest.Headers.Remove("traceparent");
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation("traceparent", activity.Id);

                if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
                {
                    transformContext.ProxyRequest.Headers.Remove("tracestate");
                    transformContext.ProxyRequest.Headers.TryAddWithoutValidation("tracestate", activity.TraceStateString);
                }
            }

            return ValueTask.CompletedTask;
        });
    });

builder.Services.AddAuthenticationAndAuthorizationExt(builder.Configuration);

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Custom Request Logging Ekle
app.UseCustomRequestLogging();

// OpenTelemetry Prometheus /metrics endpoint
app.UseObservability();

app.MapReverseProxy();

app.Run();
