using GameGaraj.WebUI.Extensions;
using GameGaraj.WebUI.Services.Abstract;
using GameGaraj.WebUI.Services.Concrete;
using GameGaraj.WebUI.Settings;
using GameGaraj.Shared.Logging;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// File Logger ekle
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFileLogger("WebUI");

// Options Pattern - appsettings.json'dan ayarları okuma
builder.Services.Configure<ServiceApiSettings>(builder.Configuration.GetSection("ServiceApiSettings"));

// HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Services
// builder.Services.AddScoped<IIdentityService, IdentityService>(); // Moved to ServiceExtension

// HttpClient Services
builder.Services.AddHttpClientServices(builder.Configuration);

// Authentication
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Auth/SignIn";
        options.AccessDeniedPath = "/Admin/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.Name = "GameGarajWebCookie";
    });

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// MVC
builder.Services.AddControllersWithViews();

// MassTransit
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
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
