using GameGaraj.Gateway.Extensions;
using GameGaraj.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

// Serilog Ekle
builder.AddSerilogLogging("Gateway");

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddAuthenticationAndAuthorizationExt(builder.Configuration);

var app = builder.Build();

app.UseRouting();

// Custom Request Logging Ekle
app.UseCustomRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();
