using GameGaraj.WebUI.Extensions;
using GameGaraj.WebUI.Services.Abstract;
using GameGaraj.WebUI.Services.Concrete;
using GameGaraj.WebUI.Settings;
using GameGaraj.Shared.Logging;
using GameGaraj.Shared.Observability;
using MassTransit;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using AspNetCoreHero.ToastNotification;
using AspNetCoreHero.ToastNotification.Extensions;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;
using Microsoft.AspNetCore.HttpOverrides;

var cultureInfo = new System.Globalization.CultureInfo("tr-TR");
cultureInfo.NumberFormat.NumberDecimalSeparator = ".";
cultureInfo.NumberFormat.NumberGroupSeparator = ",";
cultureInfo.NumberFormat.CurrencyDecimalSeparator = ".";
cultureInfo.NumberFormat.CurrencyGroupSeparator = ",";
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

var builder = WebApplication.CreateBuilder(args);

// Serilog Ekle
builder.AddSerilogLogging("WebUI");
builder.AddObservability(ObservabilityConstants.WebUIService);

// Options Pattern - appsettings.json'dan ayarları okuma
builder.Services.Configure<ServiceApiSettings>(builder.Configuration.GetSection("ServiceApiSettings"));
builder.Services.Configure<ObservabilitySettings>(builder.Configuration.GetSection("ObservabilitySettings"));

// HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Services
// builder.Services.AddScoped<IIdentityService, IdentityService>(); // Moved to ServiceExtension

// HttpClient Services
builder.Services.AddHttpClientServices(builder.Configuration);

var serviceApiSettings = builder.Configuration.GetSection("ServiceApiSettings").Get<ServiceApiSettings>() ?? new ServiceApiSettings();

// Data Protection via Redis
var redisUrl = builder.Configuration.GetConnectionString("Redis") ?? builder.Configuration["RedisUrl"];
var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("GameGarajWebUI");

if (!string.IsNullOrWhiteSpace(redisUrl) &&
    !redisUrl.Contains("abortConnect=false", StringComparison.OrdinalIgnoreCase))
{
    redisUrl = redisUrl.Contains('?') ? $"{redisUrl}&abortConnect=false" : $"{redisUrl},abortConnect=false";
}

  try
  {
      if (string.IsNullOrWhiteSpace(redisUrl))
      {
          throw new InvalidOperationException("Redis connection is not configured.");
      }

      var redisOptions = ConfigurationOptions.Parse(redisUrl);
      redisOptions.AbortOnConnectFail = true; // Hızlı hata fırlatması için
      redisOptions.ConnectTimeout = 3000; // 3 saniye içinde bağlanamazsa pes et
      var redis = ConnectionMultiplexer.Connect(redisOptions);
      
      dataProtectionBuilder.PersistKeysToStackExchangeRedis(redis, "DataProtection-Keys");
      builder.Services.AddStackExchangeRedisCache(options =>
      {
          options.Configuration = redisUrl;
          options.InstanceName = "GameGarajWebUI:";
      });
  }
  catch (Exception ex)
  {
      if (!builder.Environment.IsDevelopment())
      {
          throw new InvalidOperationException(
              "Redis DataProtection is required in non-development environments. Refusing to start with ephemeral cookie keys.",
              ex);
      }

      var keyPath = builder.Configuration["DataProtection:KeysPath"]
          ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
      Directory.CreateDirectory(keyPath);

      Console.WriteLine($"[WARNING] Redis for DataProtection failed: {ex.Message}. Persisting local development keys to {keyPath}.");
      dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
      builder.Services.AddDistributedMemoryCache();
  }

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
        
        // Güvenlik (Security) Ayarları: XSS ve CSRF koruması
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always; // HTTPS zorunlu
        options.Cookie.SameSite = SameSiteMode.Lax; // CSRF koruması

        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                if (context.Principal?.Identity?.IsAuthenticated != true)
                {
                    return;
                }

                // Get the token expiration time from the ticket
                var expiresAtToken = context.Properties.GetTokens().FirstOrDefault(t => t.Name == "expires_at")?.Value;
                if (string.IsNullOrEmpty(expiresAtToken))
                {
                    return;
                }

                if (DateTime.TryParse(expiresAtToken, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiresAt))
                {
                    // Check if token has expired or is about to expire in less than 60 seconds
                    if (expiresAt.ToUniversalTime() - DateTime.UtcNow < TimeSpan.FromSeconds(60))
                    {
                        var refreshToken = context.Properties.GetTokens().FirstOrDefault(t => t.Name == "refresh_token")?.Value;
                        var identityService = context.HttpContext.RequestServices.GetRequiredService<IIdentityService>();
                        var tokenResponse = await identityService.GetAccessTokenByRefreshTokenAsync(refreshToken);

                        if (tokenResponse != null)
                        {
                            // Update tokens inside ticket properties
                            var tokens = context.Properties.GetTokens().ToList();
                            
                            var accessTokenToken = tokens.FirstOrDefault(t => t.Name == "access_token");
                            if (accessTokenToken != null) accessTokenToken.Value = tokenResponse.AccessToken;
                            
                            var refreshTokenToken = tokens.FirstOrDefault(t => t.Name == "refresh_token");
                            if (refreshTokenToken != null) refreshTokenToken.Value = tokenResponse.RefreshToken;
                            
                            var newExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString("o", System.Globalization.CultureInfo.InvariantCulture);
                            var expiresAtItem = tokens.FirstOrDefault(t => t.Name == "expires_at");
                            if (expiresAtItem != null) expiresAtItem.Value = newExpiresAt;

                            context.Properties.StoreTokens(tokens);
                            context.ShouldRenew = true;
                        }
                        else
                        {
                            // Refresh failed, reject the principal (forces logout)
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                    }
                }
            }
        };
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

// AspNetCoreHero ToastNotification
builder.Services.AddNotyf(config =>
{
    config.DurationInSeconds = 5;
    config.IsDismissable = true;
    config.Position = NotyfPosition.BottomRight;
});

// MVC
builder.Services.AddControllersWithViews();

// Routing - SEO için tüm URL'lerin küçük harf olmasını sağlar
builder.Services.AddRouting(options => options.LowercaseUrls = true);

// Proxy'lerden gelen X-Forwarded-Proto ve X-Forwarded-For başlıklarını kabul etmesi için
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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

app.UseForwardedHeaders();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();
app.UseObservability();

app.UseNotyf();

// Custom Request Logging Ekle
app.UseCustomRequestLogging();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
