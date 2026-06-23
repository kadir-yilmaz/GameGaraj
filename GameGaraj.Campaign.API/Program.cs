using GameGaraj.Campaign.API.Rules;
using GameGaraj.Campaign.API.Services;
using GameGaraj.Campaign.API.Services.Abstract;
using GameGaraj.Campaign.API.Services.Concrete;
using GameGaraj.Shared.Logging;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Serilog Ekle
builder.AddSerilogLogging("Campaign.API");

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS — WebUI'dan erişim için
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
            e.ConfigureConsumer<GameGaraj.Campaign.API.Consumers.CouponRewardTriggeredConsumer>(context);
        });
    });
});

// Run Migration — campaign_rule, coupons, rewards, notifications, purchase logs tablolarını oluştur
DbMigrationHelper.EnsureDatabaseSetup(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseRouting();
app.UseCustomRequestLogging();

app.MapControllers();

app.Run();
