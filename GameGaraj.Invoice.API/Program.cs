using MassTransit;
using GameGaraj.Invoice.API.Consumers;
using GameGaraj.Invoice.API.Services;
using GameGaraj.Shared.Logging;
using GameGaraj.Shared.Observability;
using GameGaraj.Shared.Observability.Metrics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Minio;

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

void LoadDotEnv()
{
    var root = Directory.GetCurrentDirectory();
    var filePath = Path.Combine(root, ".env");

    if (!File.Exists(filePath))
    {
        var parent = Directory.GetParent(root);
        if (parent != null)
            filePath = Path.Combine(parent.FullName, ".env");
    }

    if (!File.Exists(filePath)) return;

    foreach (var line in File.ReadAllLines(filePath))
    {
        var parts = line.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) continue;

        var key = parts[0].Trim();
        var value = parts[1].Trim();

        if (key.StartsWith("#")) continue;

        Environment.SetEnvironmentVariable(key, value);
    }
}

// Serilog Ekle
builder.AddSerilogLogging("Invoice.API");

// OpenTelemetry (Tracing + Metrics)
builder.AddObservability(ObservabilityConstants.InvoiceService);

// Custom Business Metrics
builder.Services.AddSingleton<InvoiceMetrics>();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityOption:Authority"];
        options.Audience = builder.Configuration["IdentityOption:Audience"];
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();

// PDF Generator Service
builder.Services.AddScoped<IPdfGenerator, PdfSharpGenerator>();

// Storage Service (MinIO or LocalStorage)
var useLocalStorage = builder.Configuration.GetValue<bool>("Minio:UseLocalStorage");
var minioEndpoint = builder.Configuration["Minio:Endpoint"];

if (useLocalStorage)
{
    builder.Services.AddScoped<IStorageService, LocalStorageService>();
}
else
{
    if (string.IsNullOrWhiteSpace(minioEndpoint))
    {
        throw new InvalidOperationException("Minio endpoint is required unless Minio:UseLocalStorage is true.");
    }

    builder.Services.AddScoped<IMinioClient>(sp =>
    {
        var endpoint = builder.Configuration["Minio:Endpoint"] ?? string.Empty;
        var accessKey = builder.Configuration["Minio:AccessKey"] ?? string.Empty;
        var secretKey = builder.Configuration["Minio:SecretKey"] ?? string.Empty;
        var secure = bool.TryParse(builder.Configuration["Minio:Secure"], out bool sec) && sec;

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Minio credentials are required.");
        }

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            secure = endpointUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            endpoint = endpointUri.IsDefaultPort
                ? endpointUri.Host
                : $"{endpointUri.Host}:{endpointUri.Port}";
        }

        var client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey);

        if (secure)
        {
            client.WithSSL();
        }

        return client.Build();
    });

    builder.Services.AddScoped<IStorageService, MinioStorageService>();
}

// MassTransit + RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<InvoiceRequestedConsumer>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQUrl"], "/", host =>
        {
            host.Username("guest");
            host.Password("guest");
        });
        
        cfg.ReceiveEndpoint("invoice-requested-service", e =>
        {
            e.ConfigureConsumer<InvoiceRequestedConsumer>(context);
        });
    });
});

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

// OpenTelemetry Prometheus /metrics endpoint
app.UseObservability();

app.MapControllers();

app.Run();
