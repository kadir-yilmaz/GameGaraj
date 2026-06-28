using System.Security.Claims;
using System.Text.Json;
using GameGaraj.Review.API.Data;
using GameGaraj.Review.API.Services;
using GameGaraj.Shared.Logging;
using GameGaraj.Shared.Observability;
using GameGaraj.Shared.Observability.Metrics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddSerilogLogging("Review.API");

// OpenTelemetry (Tracing + Metrics)
builder.AddObservability(ObservabilityConstants.ReviewService);

// Custom Business Metrics
builder.Services.AddSingleton<ReviewMetrics>();

builder.Services.AddDbContext<ReviewDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<IOrderOwnershipClient, OrderOwnershipClient>(client =>
{
    var orderApiBaseUrl = builder.Configuration["ServiceUrls:OrderApi"] ?? "http://order-api:8080";
    client.BaseAddress = new Uri(orderApiBaseUrl.TrimEnd('/') + "/api/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<IContentModerationService, ContentModerationService>();
builder.Services.AddScoped<IReviewService, ReviewService>();

builder.Services.AddControllers();

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityOption:Authority"];
        options.Audience = builder.Configuration["IdentityOption:Audience"];
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateAudience = false,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is not ClaimsIdentity identity)
                {
                    return Task.CompletedTask;
                }

                var realmAccessClaim = identity.FindFirst("realm_access")?.Value;
                if (string.IsNullOrWhiteSpace(realmAccessClaim))
                {
                    return Task.CompletedTask;
                }

                try
                {
                    using var document = JsonDocument.Parse(realmAccessClaim);
                    if (document.RootElement.TryGetProperty("roles", out var roles))
                    {
                        foreach (var role in roles.EnumerateArray())
                        {
                            var roleValue = role.GetString();
                            if (!string.IsNullOrWhiteSpace(roleValue) && !identity.HasClaim(ClaimTypes.Role, roleValue))
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, roleValue));
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore malformed role payloads; authorization will fail normally.
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseCustomRequestLogging();

// OpenTelemetry Prometheus /metrics endpoint
app.UseObservability();

app.MapControllers();

app.Run();
