using Microsoft.EntityFrameworkCore;
using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Repositories.Abstract;
using GameGaraj.Catalog.API.Repositories.Postgres;
using GameGaraj.Catalog.API.Services.Abstract;
using GameGaraj.Catalog.API.Services.Concrete;
using MassTransit;
using GameGaraj.Catalog.API.Consumers;
using GameGaraj.Shared.Logging;
using Npgsql;
using Elastic.Clients.Elasticsearch;
using GameGaraj.Catalog.API.Models;

var builder = WebApplication.CreateBuilder(args);

// File Logger ekle
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFileLogger("Catalog.API");

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Repositories Configuration (PostgreSQL)
var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("PostgresConnection"));
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(dataSource));

builder.Services.AddScoped<IProductRepository, PostgresProductRepository>();
builder.Services.AddScoped<ICategoryRepository, PostgresCategoryRepository>();
builder.Services.AddScoped<IAttributeRepository, PostgresAttributeRepository>();

// ElasticSearch Configuration
var elasticUri = builder.Configuration["ElasticSearchSettings:Uri"] ?? "http://localhost:9200";
var defaultIndex = builder.Configuration["ElasticSearchSettings:DefaultIndex"] ?? "products";

var settings = new ElasticsearchClientSettings(new Uri(elasticUri))
    .DefaultIndex(defaultIndex);

var elasticClient = new ElasticsearchClient(settings);
builder.Services.AddSingleton(elasticClient);

// Services
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();

// Controllers
builder.Services.AddControllers();

// Authentication
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityOption:Authority"];
        options.Audience = builder.Configuration["IdentityOption:Audience"];
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// MassTransit Configuration
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderStartedConsumer>();
    x.AddConsumer<PaymentCompletedConsumer>();
    x.AddConsumer<PaymentFailedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqUrl = builder.Configuration["RabbitMQUrl"];
        if (string.IsNullOrEmpty(rabbitMqUrl))
        {
            Console.WriteLine("[Catalog API Error] RabbitMQUrl is not configured in appsettings.json!");
            rabbitMqUrl = "localhost"; // Fallback
        }

        cfg.Host(rabbitMqUrl, "/", host =>
        {
            host.Username("guest");
            host.Password("guest");
        });

        cfg.ReceiveEndpoint("order-started-catalog-service", e =>
        {
            e.ConfigureConsumer<OrderStartedConsumer>(context);
        });

        cfg.ReceiveEndpoint("payment-completed-catalog-service", e =>
        {
            e.ConfigureConsumer<PaymentCompletedConsumer>(context);
        });

        cfg.ReceiveEndpoint("payment-failed-catalog-service", e =>
        {
            e.ConfigureConsumer<PaymentFailedConsumer>(context);
        });
    });
});

var app = builder.Build();

// PostgreSQL Startup Tasks
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await dbContext.Database.MigrateAsync();
    
    // Seed Data into Postgres
    await GameGaraj.Catalog.API.Services.Seed.PostgresDbSeeder.SeedAsync(app.Services);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
