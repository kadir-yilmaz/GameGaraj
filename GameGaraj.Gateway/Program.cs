using GameGaraj.Gateway.Extensions;
using GameGaraj.Shared.Observability;
using System.Diagnostics;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

// YÃ¼ksek anlÄ±k yÃ¼k testleri (Load Test) iÃ§in ThreadPool'u baÅŸtan geniÅŸletiyoruz
System.Threading.ThreadPool.SetMinThreads(1000, 1000);

// Serilog Ekle

// OpenTelemetry (Tracing + Metrics)

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


// OpenTelemetry Prometheus /metrics endpoint

app.MapReverseProxy();

app.Run();
