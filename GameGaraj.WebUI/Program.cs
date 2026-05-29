using GameGaraj.WebUI.Extensions;
using GameGaraj.WebUI.Services.Abstract;
using GameGaraj.WebUI.Services.Concrete;
using GameGaraj.WebUI.Settings;
using GameGaraj.Shared.Logging;
using MassTransit;
using System.Text.Json;

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

// Admin Seed İşlemi
await SeedAdminUserAsync(app.Services, builder.Configuration);

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

// Admin Seed Fonksiyonu
async Task SeedAdminUserAsync(IServiceProvider serviceProvider, IConfiguration configuration)
{
    try
    {
        // Environment variable'lardan oku
        var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

        if (string.IsNullOrEmpty(adminEmail) || string.IsNullOrEmpty(adminPassword))
        {
            Console.WriteLine("[Seed-Admin] ⚠️ ADMIN_EMAIL veya ADMIN_PASSWORD environment variable'ları tanımlanmamış");
            return;
        }

        using var scope = serviceProvider.CreateScope();
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient();
        var serviceApiSettings = configuration.GetSection("ServiceApiSettings").Get<ServiceApiSettings>();

        if (serviceApiSettings == null)
        {
            Console.WriteLine("[Seed-Admin] ✗ ServiceApiSettings not found");
            return;
        }

        // 1. Admin token al
        var adminTokenEndpoint = $"{serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/realms/master/protocol/openid-connect/token";
        
        var adminTokenContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "client_id", "admin-cli" },
            { "grant_type", "password" },
            { "username", "admin" },
            { "password", "admin" }
        });

        var adminTokenResponse = await httpClient.PostAsync(adminTokenEndpoint, adminTokenContent);
        if (!adminTokenResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("[Seed-Admin] ✗ Failed to get admin token from Keycloak");
            return;
        }

        var adminTokenJson = await adminTokenResponse.Content.ReadAsStringAsync();
        var adminToken = JsonSerializer.Deserialize<TokenResponse>(adminTokenJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (adminToken == null)
        {
            Console.WriteLine("[Seed-Admin] ✗ Failed to parse admin token");
            return;
        }

        // 2. Kullanıcıyı kontrol et
        var getUsersEndpoint = $"{serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj/users?email={Uri.EscapeDataString(adminEmail)}";
        
        var getUsersRequest = new HttpRequestMessage(HttpMethod.Get, getUsersEndpoint);
        getUsersRequest.Headers.Add("Authorization", $"Bearer {adminToken.AccessToken}");

        var getUsersResponse = await httpClient.SendAsync(getUsersRequest);
        var getUsersJson = await getUsersResponse.Content.ReadAsStringAsync();
        var existingUsers = JsonSerializer.Deserialize<List<JsonElement>>(getUsersJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (existingUsers != null && existingUsers.Count > 0)
        {
            Console.WriteLine($"[Seed-Admin] ✓ Admin user '{adminEmail}' already exists");
            return;
        }

        // 3. Kullanıcıyı oluştur
        var createUserEndpoint = $"{serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj/users";
        
        var userData = new
        {
            username = adminEmail,
            email = adminEmail,
            firstName = "Admin",
            lastName = "User",
            enabled = true,
            emailVerified = true,
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = adminPassword,
                    temporary = false
                }
            }
        };

        var createUserRequest = new HttpRequestMessage(HttpMethod.Post, createUserEndpoint);
        createUserRequest.Headers.Add("Authorization", $"Bearer {adminToken.AccessToken}");
        createUserRequest.Content = new StringContent(
            JsonSerializer.Serialize(userData),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var createUserResponse = await httpClient.SendAsync(createUserRequest);
        
        if (!createUserResponse.IsSuccessStatusCode)
        {
            var errorContent = await createUserResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"[Seed-Admin] ✗ Failed to create admin user: {errorContent}");
            return;
        }

        // 4. Admin rolünü ata
        var getUserIdEndpoint = $"{serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj/users?email={Uri.EscapeDataString(adminEmail)}";
        
        var getNewUserRequest = new HttpRequestMessage(HttpMethod.Get, getUserIdEndpoint);
        getNewUserRequest.Headers.Add("Authorization", $"Bearer {adminToken.AccessToken}");

        var getNewUserResponse = await httpClient.SendAsync(getNewUserRequest);
        var getNewUserJson = await getNewUserResponse.Content.ReadAsStringAsync();
        var newUsers = JsonSerializer.Deserialize<List<JsonElement>>(getNewUserJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (newUsers == null || newUsers.Count == 0)
        {
            Console.WriteLine("[Seed-Admin] ✗ Failed to get newly created user ID");
            return;
        }

        var userId = newUsers[0].GetProperty("id").GetString();

        // Admin rolünü ata
        var assignRoleEndpoint = $"{serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj/users/{userId}/role-mappings/realm";
        
        var roleData = new[]
        {
            new
            {
                id = "admin-role-id",
                name = "admin",
                composite = false,
                clientRole = false
            }
        };

        var assignRoleRequest = new HttpRequestMessage(HttpMethod.Post, assignRoleEndpoint);
        assignRoleRequest.Headers.Add("Authorization", $"Bearer {adminToken.AccessToken}");
        assignRoleRequest.Content = new StringContent(
            JsonSerializer.Serialize(roleData),
            System.Text.Encoding.UTF8,
            "application/json"
        );

        var assignRoleResponse = await httpClient.SendAsync(assignRoleRequest);
        
        if (assignRoleResponse.IsSuccessStatusCode)
        {
            Console.WriteLine($"[Seed-Admin] ✅ Admin user '{adminEmail}' created successfully with admin role");
        }
        else
        {
            Console.WriteLine($"[Seed-Admin] ⚠️ Admin user created but role assignment may have failed");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Seed-Admin] ✗ Error during admin seed: {ex.Message}");
    }
}

// TokenResponse modeli
public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}
