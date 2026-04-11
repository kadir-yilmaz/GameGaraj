using Microsoft.EntityFrameworkCore;
using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Repositories.Abstract;
using GameGaraj.Catalog.API.Repositories.Mongo;
using GameGaraj.Catalog.API.Repositories.Postgres;
using GameGaraj.Catalog.API.Services;
using GameGaraj.Catalog.API.Services.Abstract;
using GameGaraj.Catalog.API.Services.Concrete;
using MassTransit;
using GameGaraj.Catalog.API.Consumers;
using GameGaraj.Shared.Logging;
using GameGaraj.Catalog.API.Enums;
using Npgsql;
using Elastic.Clients.Elasticsearch;
using MongoDB.Driver;
using GameGaraj.Catalog.API.Models;

var builder = WebApplication.CreateBuilder(args);

// File Logger ekle
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFileLogger("Catalog.API");

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Repositories Configuration
var databaseProviderString = builder.Configuration["DatabaseProvider"] ?? "Mongo";
if (!Enum.TryParse<DatabaseProviderType>(databaseProviderString, true, out var databaseProvider))
{
    databaseProvider = DatabaseProviderType.Mongo;
}

if (databaseProvider == DatabaseProviderType.Postgres)
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("PostgresConnection"));
    dataSourceBuilder.EnableDynamicJson();
    var dataSource = dataSourceBuilder.Build();

    builder.Services.AddDbContext<CatalogDbContext>(options =>
        options.UseNpgsql(dataSource));

    builder.Services.AddScoped<IProductRepository, PostgresProductRepository>();
    builder.Services.AddScoped<ICategoryRepository, PostgresCategoryRepository>();
    builder.Services.AddScoped<IAttributeRepository, PostgresAttributeRepository>();
}
else
{
    // MongoDB Settings
    builder.Services.Configure<MongoDbSettings>(
        builder.Configuration.GetSection("MongoDbSettings"));

    // MongoDB Client
    var mongoConnectionString = builder.Configuration["MongoDbSettings:ConnectionString"];
    builder.Services.AddSingleton<IMongoClient>(new MongoClient(mongoConnectionString));

    // MongoDB Context
    builder.Services.AddScoped<MongoDbContext>();

    builder.Services.AddScoped<IProductRepository, MongoProductRepository>();
    builder.Services.AddScoped<ICategoryRepository, MongoCategoryRepository>();
    builder.Services.AddScoped<IAttributeRepository, MongoAttributeRepository>();
}

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

var selectedProviderString = app.Configuration["DatabaseProvider"] ?? "Mongo";
if (!Enum.TryParse<DatabaseProviderType>(selectedProviderString, true, out var selectedProvider))
{
    selectedProvider = DatabaseProviderType.Mongo;
}

if (selectedProvider == DatabaseProviderType.Postgres)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await dbContext.Database.MigrateAsync(); // Applies pending EF Core migrations automatically
    
    // Seed Data into Postgres
    await GameGaraj.Catalog.API.Services.Seed.PostgresDbSeeder.SeedAsync(app.Services);
}
else
{
    // Seed Data into Mongo
    await GameGaraj.Catalog.API.Services.Seed.MongoDbSeeder.SeedAsync(app.Services);

    // Create Indexes
    await DatabaseIndexHelper.CreateIndexesAsync(app.Services);

    // Verify Indexes (Development only)
    if (app.Environment.IsDevelopment())
    {
        await IndexVerificationHelper.VerifyIndexesAsync(app.Services);
    }
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

// Development-only endpoint to reset database
if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/dev/sync-elasticsearch", async (
        GameGaraj.Catalog.API.Repositories.Abstract.IProductRepository repo, 
        Elastic.Clients.Elasticsearch.ElasticsearchClient elasticClient) =>
    {
        var products = await repo.GetAllAsync();
        var errors = new List<string>();
        foreach (var p in products)
        {
            var request = new Elastic.Clients.Elasticsearch.IndexRequest<Product>(p, "products", p.Id);
            var response = await elasticClient.IndexAsync(request);
            if (!response.IsValidResponse)
            {
                errors.Add($"Failed for ID {p.Id}: {response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation}");
            }
        }
        return Results.Ok(new { message = $"Synchronized {products.Count} products to Elasticsearch.", errors });
    })
    .WithName("SyncElasticsearch");

    if (selectedProvider == DatabaseProviderType.Postgres)
    {
        app.MapPost("/api/dev/reset-database", async (CatalogDbContext dbContext, IServiceProvider services) =>
        {
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
            await GameGaraj.Catalog.API.Services.Seed.PostgresDbSeeder.SeedAsync(services);
            return Results.Ok(new { message = "Postgres database reset and re-seeded successfully." });
        })
        .WithName("ResetDatabase");
        
        app.MapGet("/api/dev/inspect-database", async (CatalogDbContext dbContext) =>
        {
            var productCount = await dbContext.Products.CountAsync();
            var categoryCount = await dbContext.Categories.CountAsync();

            var sampleProducts = await dbContext.Products.Take(5).Select(p => new
            {
                id = p.Id.ToString(),
                name = p.Name,
                categoryId = p.CategoryId.ToString()
            }).ToListAsync();

            var allCategories = await dbContext.Categories.Select(c => new
            {
                id = c.Id.ToString(),
                name = c.Name,
                parentId = c.ParentId != null ? c.ParentId.ToString() : null
            }).ToListAsync();

            return Results.Ok(new
            {
                database = "Postgres",
                productCount,
                categoryCount,
                sampleProducts,
                categories = allCategories
            });
        })
        .WithName("InspectDatabase");
    }
    else
    {
        app.MapPost("/api/dev/reset-database", async (IMongoClient mongoClient, IConfiguration config) =>
        {
            var dbName = config["MongoDbSettings:DatabaseName"];
            var db = mongoClient.GetDatabase(dbName);

            await db.DropCollectionAsync("products");
            await db.DropCollectionAsync("categories");
            await db.DropCollectionAsync("categoryAttributes");
            await db.DropCollectionAsync("_seed_metadata");

            return Results.Ok(new { message = "Mongo database reset successfully. Restart the API to re-seed." });
        })
        .WithName("ResetDatabase");

        // Debug endpoint to inspect database state
        app.MapGet("/api/dev/inspect-database", async (IMongoClient mongoClient, IConfiguration config) =>
        {
            var dbName = config["MongoDbSettings:DatabaseName"];
            var db = mongoClient.GetDatabase(dbName);

            var productsCollection = db.GetCollection<MongoDB.Bson.BsonDocument>("products");
            var categoriesCollection = db.GetCollection<MongoDB.Bson.BsonDocument>("categories");

            var productCount = await productsCollection.CountDocumentsAsync(new MongoDB.Bson.BsonDocument());
            var categoryCount = await categoriesCollection.CountDocumentsAsync(new MongoDB.Bson.BsonDocument());

            // Get sample products with their CategoryId
            var sampleProducts = await productsCollection.Find(new MongoDB.Bson.BsonDocument())
                .Limit(5)
                .ToListAsync();

            // Get all categories
            var allCategories = await categoriesCollection.Find(new MongoDB.Bson.BsonDocument())
                .ToListAsync();

            return Results.Ok(new
            {
                database = dbName,
                productCount,
                categoryCount,
                sampleProducts = sampleProducts.Select(p => new
                {
                    id = p["_id"].ToString(),
                    name = p.Contains("Name") ? p["Name"].AsString : "N/A",
                    categoryId = p.Contains("CategoryId") ? p["CategoryId"].ToString() : "N/A",
                    categoryIdType = p.Contains("CategoryId") ? p["CategoryId"].BsonType.ToString() : "N/A"
                }),
                categories = allCategories.Select(c => new
                {
                    id = c["_id"].ToString(),
                    name = c.Contains("Name") ? c["Name"].AsString : "N/A",
                    parentId = c.Contains("ParentId") && !c["ParentId"].IsBsonNull ? c["ParentId"].ToString() : null
                })
            });
        })
        .WithName("InspectDatabase");
    }
}

app.Run();

