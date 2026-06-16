using GameGaraj.WebUI.Extensions;
using GameGaraj.WebUI.Services.Abstract;
using GameGaraj.WebUI.Services.Concrete;
using GameGaraj.WebUI.Settings;
using GameGaraj.Shared.Logging;
using MassTransit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Security.Claims;
using System.Text.Json;

var cultureInfo = new System.Globalization.CultureInfo("tr-TR");
cultureInfo.NumberFormat.NumberDecimalSeparator = ".";
cultureInfo.NumberFormat.NumberGroupSeparator = ",";
cultureInfo.NumberFormat.CurrencyDecimalSeparator = ".";
cultureInfo.NumberFormat.CurrencyGroupSeparator = ",";
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

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

var serviceApiSettings = builder.Configuration.GetSection("ServiceApiSettings").Get<ServiceApiSettings>() ?? new ServiceApiSettings();

// Authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Auth/SignIn";
        options.AccessDeniedPath = "/Admin/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.Name = "GameGarajWebCookie";
    })
    .AddOpenIdConnect("Keycloak", options =>
    {
        options.Authority = serviceApiSettings.IdentityBaseUri;
        options.ClientId = "web-ui";
        options.ResponseType = "code";
        options.RequireHttpsMetadata = false;
        options.SaveTokens = true;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.CallbackPath = "/signin-oidc";
        options.GetClaimsFromUserInfoEndpoint = true;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.TokenValidationParameters.NameClaimType = "preferred_username";
        options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                if (context.Properties.Items.TryGetValue("kc_idp_hint", out var idpHint) &&
                    !string.IsNullOrWhiteSpace(idpHint))
                {
                    context.ProtocolMessage.SetParameter("kc_idp_hint", idpHint);
                }

                if (context.Properties.Items.TryGetValue("prompt", out var prompt) &&
                    !string.IsNullOrWhiteSpace(prompt))
                {
                    context.ProtocolMessage.Prompt = prompt;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is not ClaimsIdentity identity)
                {
                    return Task.CompletedTask;
                }

                var realmAccessClaim = identity.FindFirst("realm_access")?.Value;
                if (string.IsNullOrWhiteSpace(realmAccessClaim))
                {
                    return Task.CompletedTask;
                }

                try
                {
                    var realmAccess = JsonSerializer.Deserialize<JsonElement>(realmAccessClaim);
                    if (realmAccess.ValueKind == JsonValueKind.Object &&
                        realmAccess.TryGetProperty("roles", out var roles) &&
                        roles.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var role in roles.EnumerateArray()
                            .Select(item => item.GetString())
                            .Where(item => !string.IsNullOrWhiteSpace(item)))
                        {
                            if (!identity.HasClaim(ClaimTypes.Role, role!))
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, role!));
                            }
                        }
                    }
                }
                catch
                {
                    // Keep login flow alive even if role parsing fails.
                }

                return Task.CompletedTask;
            }
        };
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

// Routing - SEO için tüm URL'lerin küçük harf olmasını sağlar
builder.Services.AddRouting(options => options.LowercaseUrls = true);

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
