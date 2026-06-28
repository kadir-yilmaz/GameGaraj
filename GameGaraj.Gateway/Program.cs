using GameGaraj.Gateway.Extensions;
using GameGaraj.Shared.Logging;
using GameGaraj.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

// Serilog Ekle
builder.AddSerilogLogging("Gateway");

// OpenTelemetry (Tracing + Metrics)
builder.AddObservability(ObservabilityConstants.GatewayService);

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

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
