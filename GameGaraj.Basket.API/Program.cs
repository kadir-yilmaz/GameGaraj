using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.Extensions.DependencyInjection;
using GameGaraj.Shared.Logging;

using GameGaraj.Basket.API.Features.Baskets.DeleteBasket;
using GameGaraj.Basket.API.Features.Baskets.GetBasket;
using GameGaraj.Basket.API.Features.Baskets.AddBasketItem;
using GameGaraj.Basket.API.Services;
using GameGaraj.Basket.API.Features.Baskets.DeleteBasketItem;
using GameGaraj.Basket.API.Features.Favorites.GetFavorites;
using GameGaraj.Basket.API.Features.Favorites.AddFavorite;
using GameGaraj.Basket.API.Features.Favorites.RemoveFavorite;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Serilog Ekle
builder.AddSerilogLogging("Basket.API");

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Redis Cache - Direkt Master'a bağlan
builder.Services.AddStackExchangeRedisCache(options =>
{
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    options.Configuration = redisConnection;
});

// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<BasketService>();
builder.Services.AddScoped<FavoritesService>();
builder.Services.AddMediatR(cfg => 
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenBehavior(typeof(GameGaraj.Basket.API.Behaviors.ValidationBehavior<,>));
});

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityOption:Authority"];
        options.Audience = builder.Configuration["IdentityOption:Audience"];
        options.RequireHttpsMetadata = false;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
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

// Versioning Set
var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

// Map Endpoints
var basketGroup = app.MapGroup("api/v{version:apiVersion}/baskets")
    .WithApiVersionSet(apiVersionSet)
    .WithTags("Baskets");

// Wire up features
basketGroup.MapGetBasketEndpoint()
           .MapAddBasketItemEndpoint()
           .MapDeleteBasketEndpoint()
           .MapDeleteBasketItemEndpoint();

// Map Favorites Endpoints
var favoritesGroup = app.MapGroup("api/v{version:apiVersion}/favorites")
    .WithApiVersionSet(apiVersionSet)
    .WithTags("Favorites");

favoritesGroup.MapGetFavoritesEndpoint()
              .MapAddFavoriteEndpoint()
              .MapRemoveFavoriteEndpoint();

app.Run();
