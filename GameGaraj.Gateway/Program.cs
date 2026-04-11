using GameGaraj.Gateway.Extensions;
using GameGaraj.Shared.Logging;

var builder = WebApplication.CreateBuilder(args);

// File Logger ekle
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFileLogger("Gateway");

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddAuthenticationAndAuthorizationExt(builder.Configuration);

var app = builder.Build();

app.MapReverseProxy();

app.UseAuthentication();
app.UseAuthorization();

app.Run();
