using GameGaraj.Campaign.API.Rules;
using GameGaraj.Campaign.API.Services;
using GameGaraj.Campaign.API.Services.Abstract;
using GameGaraj.Campaign.API.Services.Concrete;
using GameGaraj.Shared.Observability;
using GameGaraj.Shared.Observability.Metrics;
using GameGaraj.Shared.Chaos;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Elasticsearch Logging (tek sabit index: gamegaraj-campaign-api-logs)
builder.AddSimpleElasticLogging("campaign-api");

// OpenTelemetry (Tracing + Metrics)
builder.AddPrometheusMetrics("campaign-api");

// Custom Business Metrics
builder.Services.AddSingleton<CampaignMetrics>();

// Chaos Engine
builder.Services.AddChaosServices();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS â€” WebUI'dan eriÅŸim iÃ§in
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Services (Dapper CRUD)
builder.Services.AddScoped<ICampaignRuleService, CampaignRuleService>();
builder.Services.AddScoped<ICampaignCalculationService, CampaignCalculationService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<ICouponRewardService, CouponRewardService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICarouselImageService, CarouselImageService>();

// Strategy Pattern: Kural stratejilerini DI'a kaydet
builder.Services.AddSingleton<ICampaignRule, TotalAmountRule>();
builder.Services.AddSingleton<ICampaignRule, BuyXGetYFreeRule>();
builder.Services.AddSingleton<ICampaignRule, CheapestItemDiscountRule>();
builder.Services.AddSingleton<ICampaignRule, BrandDiscountRule>();

// MassTransit Configuration
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<GameGaraj.Campaign.API.Consumers.CouponRewardTriggeredConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqUrl = builder.Configuration["RabbitMQUrl"];
        if (string.IsNullOrEmpty(rabbitMqUrl))
        {
            rabbitMqUrl = "localhost";
        }

        cfg.Host(rabbitMqUrl, "/", host =>
        {
            host.Username("guest");
            host.Password("guest");
        });

        cfg.ReceiveEndpoint("coupon-reward-triggered-campaign-service", e =>
        {
            e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200)));
            e.ConfigureConsumer<GameGaraj.Campaign.API.Consumers.CouponRewardTriggeredConsumer>(context);
        });
    });
});

// Run Migration â€” campaign_rule, coupons, rewards, notifications, purchase logs tablolarÄ±nÄ± oluÅŸtur
var app = builder.Build();

// Non-blocking DB setup with background retry
_ = Task.Run(async () =>
{
    while (true)
    {
        try
        {
            DbMigrationHelper.EnsureDatabaseSetup(app.Configuration);
            app.Logger.LogInformation("[Startup] Campaign DB setup completed.");
            break;
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "[Startup] Campaign DB unreachable, retrying in 15s...");
            await Task.Delay(TimeSpan.FromSeconds(15));
        }
    }
});

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseRouting();

// Chaos Testing Engine
app.UseChaos("campaign");

// OpenTelemetry Prometheus /metrics endpoint
app.UsePrometheusMetrics();

app.MapControllers();

app.Run();
