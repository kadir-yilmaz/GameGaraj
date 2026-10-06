using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using GameGaraj.Discussion.API.Consumers;
using GameGaraj.Discussion.API.Data;
using GameGaraj.Discussion.API.Hubs;
using GameGaraj.Discussion.API.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Serilog Ekle

// PostgreSQL
builder.Services.AddDbContext<DiscussionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

// Redis Cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "discussion-api:";
});

// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IContentModerationService, ContentModerationService>();
builder.Services.AddScoped<IDiscussionService, DiscussionService>();

builder.Services.AddControllers()
    .ConfigureApplicationPartManager(manager =>
    {
        manager.ApplicationParts.Clear();
        manager.ApplicationParts.Add(new Microsoft.AspNetCore.Mvc.ApplicationParts.AssemblyPart(typeof(Program).Assembly));
    });

// Authentication â€” JWT Bearer (Keycloak)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
        options.Events = new JwtBearerEvents
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
            },
            // SignalR WebSocket baÄŸlantÄ±sÄ±nda JWT token query string'den gelir
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/discussion"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// SignalR
builder.Services.AddSignalR();

// Rate Limiting (Madde 10)
builder.Services.AddRateLimiter(options =>
{
    // Soru sorma: KullanÄ±cÄ± baÅŸÄ±na 5 soru / dakika
    options.AddPolicy("question-create", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst("sub")?.Value ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));

    // Cevap verme: Admin baÅŸÄ±na 30 cevap / dakika
    options.AddPolicy("answer-create", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirst("sub")?.Value ?? "admin",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProductDeletedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqUrl = builder.Configuration["RabbitMQUrl"];
        if (string.IsNullOrEmpty(rabbitMqUrl))
        {
            Console.WriteLine("[Discussion API Error] RabbitMQUrl is not configured!");
            rabbitMqUrl = "localhost";
        }

        cfg.Host(rabbitMqUrl, "/", host =>
        {
            host.Username("guest");
            host.Password("guest");
        });

        cfg.ReceiveEndpoint("product-deleted-discussion-service", e =>
        {
            e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200)));
            e.ConfigureConsumer<ProductDeletedConsumer>(context);
        });
    });
});

// CORS (WebUI'dan SignalR baÄŸlantÄ±sÄ± iÃ§in)
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalRCors", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5001",
                "https://localhost:5001",
                "http://localhost:5000",
                "https://localhost:5000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// PostgreSQL Auto-Migrate
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DiscussionDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseCors("SignalRCors");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();

// SignalR Hub â€” Discussion API'de (Madde 9: yetkilendirilmiÅŸ baÄŸlantÄ±)
app.MapHub<DiscussionHub>("/hubs/discussion");

app.Run();
