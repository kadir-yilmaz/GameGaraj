using GameGaraj.Shared.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Minio;
using GameGaraj.PhotoStock.API.Services;

var builder = WebApplication.CreateBuilder(args);

// File Logger ekle
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFileLogger("PhotoStock.API");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register Storage Service (MinIO only)
builder.Services.AddScoped<IMinioClient>(sp =>
{
    var endpoint = builder.Configuration["Minio:Endpoint"] ?? string.Empty;
    var accessKey = builder.Configuration["Minio:AccessKey"] ?? string.Empty;
    var secretKey = builder.Configuration["Minio:SecretKey"] ?? string.Empty;
    var secure = bool.TryParse(builder.Configuration["Minio:Secure"], out bool sec) && sec;

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

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityOption:Authority"];
        options.Audience = builder.Configuration["IdentityOption:Audience"];
        options.RequireHttpsMetadata = false;
    });

builder.Services.AddAuthorization();

// Configure request size limit for file uploads (25MB total)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 25 * 1024 * 1024; // 25MB
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 25 * 1024 * 1024; // 25MB
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve static files from wwwroot (for photos)
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
