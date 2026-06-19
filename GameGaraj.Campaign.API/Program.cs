using GameGaraj.Campaign.API.Rules;
using GameGaraj.Campaign.API.Services;
using GameGaraj.Campaign.API.Services.Abstract;
using GameGaraj.Campaign.API.Services.Concrete;
using GameGaraj.Shared.Logging;

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

// Strategy Pattern: Kural stratejilerini DI'a kaydet
// Yeni kural eklemek için buraya bir satır eklenmesi yeterli (Open/Closed Principle)
builder.Services.AddSingleton<ICampaignRule, TotalAmountRule>();
builder.Services.AddSingleton<ICampaignRule, BuyXGetYFreeRule>();
builder.Services.AddSingleton<ICampaignRule, CheapestItemDiscountRule>();

// Run Migration — campaign_rule tablosunu oluştur
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
