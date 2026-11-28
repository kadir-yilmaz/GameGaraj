using System.Security.Claims;
using System.Text.Json;
using GameGaraj.Review.API.Data;
using GameGaraj.Review.API.Services;
using GameGaraj.Shared.Observability;
using GameGaraj.Shared.Observability.Metrics;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Elasticsearch Logging (tek sabit index: gamegaraj-review-api-logs)
builder.AddSimpleElasticLogging("review-api");

// OpenTelemetry (Tracing + Metrics)
builder.AddPrometheusMetrics("review-api");

// Custom Business Metrics
builder.Services.AddSingleton<ReviewMetrics>();

builder.Services.AddDbContext<ReviewDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

builder.Services.AddStackExchangeRedisCache(options =>
{
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrWhiteSpace(redisConnection))
    {
        // Secret Go servisleriyle ortak (host:port), bu yüzden abortConnect burada kodda kapatılır.
        var redisOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnection);
        redisOptions.AbortOnConnectFail = false;
        options.ConfigurationOptions = redisOptions;
    }
    else
    {
        Console.WriteLine("[Review API Warning] ConnectionStrings:Redis is not configured. Cache operations will fail and fall back to database.");
    }
    options.InstanceName = "review-api:";
});
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

// Auto Migration (non-blocking with background retry)
_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ReviewDbContext>();
            await dbContext.Database.MigrateAsync();
            app.Logger.LogInformation("[Startup] Review DB migration completed.");
            break;
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "[Startup] Review DB unreachable, retrying in 15s...");
            await Task.Delay(TimeSpan.FromSeconds(15));
        }
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// OpenTelemetry Prometheus /metrics endpoint
app.UsePrometheusMetrics();

app.MapControllers();

app.Run();
