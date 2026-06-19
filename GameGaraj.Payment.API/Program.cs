using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Mvc;
using GameGaraj.Payment.API.Settings;
using GameGaraj.Shared.Logging;

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
builder.AddSerilogLogging("Payment.API");

// Iyzipay Settings
builder.Services.Configure<IyzipaySettings>(
    builder.Configuration.GetSection("Iyzipay"));

// Add services to the container.
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new AuthorizeFilter());
})
.ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        foreach (var error in context.ModelState)
        {
            foreach (var inner in error.Value.Errors)
            {
                Console.WriteLine($"[Payment API Validation Error] Field: {error.Key}, Error: {inner.ErrorMessage}");
            }
        }
        return new BadRequestObjectResult(context.ModelState);
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityServerURL"];
        options.Audience = "resource_payment";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false
        };
    });

// MassTransit + RabbitMQ (Event publisher)
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQUrl"], "/", host =>
        {
            host.Username("guest");
            host.Password("guest");
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
app.UseCustomRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
