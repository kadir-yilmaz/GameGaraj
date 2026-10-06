using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using GameGaraj.Order.Application.Handlers;
using GameGaraj.Order.Application.Consumers;
using GameGaraj.Order.Application.Services.Abstract;
using GameGaraj.Order.Application.Services.Concrete;
using GameGaraj.Order.Application.Mapping;
using GameGaraj.Order.Infrastructure;
using GameGaraj.Order.Infrastructure.Repositories.Abstract;
using GameGaraj.Order.Infrastructure.Repositories.Concrete;
using GameGaraj.Order.API.Services.Hosted;
using Microsoft.AspNetCore.Mvc;
using MassTransit;
using GameGaraj.Shared.Observability;
using GameGaraj.Shared.Observability.Metrics;
using GameGaraj.Shared.Chaos;

var builder = WebApplication.CreateBuilder(args);

// Serilog Ekle

// OpenTelemetry (Tracing + Metrics)

// Custom Business Metrics
builder.Services.AddSingleton<OrderMetrics>();

// Chaos Engine
builder.Services.AddChaosServices();

// Add services to the container.
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");
builder.Services.AddControllers(options =>
{
    options.Filters.Add(new AuthorizeFilter());
}).ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        foreach (var error in context.ModelState)
        {
            foreach (var inner in error.Value.Errors)
            {
                Console.WriteLine($"[Order API Validation Error] Field: {error.Key}, Error: {inner.ErrorMessage}");
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
        options.Audience = "resource_order";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false
        };
    });

// DbContext
builder.Services.AddDbContext<OrderDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// AutoMapper
builder.Services.AddAutoMapper(typeof(OrderMappingProfile));

// Repositories
builder.Services.AddScoped<IUserAddressRepository, UserAddressRepository>();

// Services
builder.Services.AddScoped<IUserAddressService, UserAddressService>();
builder.Services.AddHostedService<OrderExpirationWorker>();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommandHandler).Assembly));

// MassTransit + RabbitMQ (Event consumers + Outbox)
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
        o.QueryDelay = TimeSpan.FromSeconds(1);
        o.DuplicateDetectionWindow = TimeSpan.FromMinutes(5);
    });

    x.AddConsumer<ProductNameChangedConsumer>();
    x.AddConsumer<PaymentCompletedConsumer>();
    x.AddConsumer<PaymentFailedConsumer>();
    x.AddConsumer<StockNotReservedConsumer>();
    x.AddConsumer<UserAddressSaveRequestedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQUrl"], "/", host =>
        {
            host.Username("guest");
            host.Password("guest");
        });

        cfg.ReceiveEndpoint("product-name-changed-order-service", e =>
        {
            e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200)));
            e.ConfigureConsumer<ProductNameChangedConsumer>(context);
        });

        cfg.ReceiveEndpoint("payment-completed-order-service", e =>
        {
            e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200)));
            e.ConfigureConsumer<PaymentCompletedConsumer>(context);
        });

        cfg.ReceiveEndpoint("payment-failed-order-service", e =>
        {
            e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200)));
            e.ConfigureConsumer<PaymentFailedConsumer>(context);
        });

        cfg.ReceiveEndpoint("stock-not-reserved-order-service", e =>
        {
            e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200)));
            e.ConfigureConsumer<StockNotReservedConsumer>(context);
        });

        cfg.ReceiveEndpoint("user-address-save-requested-order-service", e =>
        {
            e.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200)));
            e.ConfigureConsumer<UserAddressSaveRequestedConsumer>(context);
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

// Chaos Testing Engine
app.UseChaos("order");

// OpenTelemetry Prometheus /metrics endpoint

app.MapControllers();

// Auto Migration
using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var orderDbContext = serviceProvider.GetRequiredService<OrderDbContext>();
    orderDbContext.Database.Migrate();
}

app.Run();
