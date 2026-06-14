using GameGaraj.WebUI.Models.Auth;
using GameGaraj.WebUI.Services.Abstract;
using GameGaraj.WebUI.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace GameGaraj.WebUI.Services.Concrete
{
    public class IdentityService : IIdentityService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ServiceApiSettings _serviceApiSettings;

        // Keycloak Token Endpoint Sabitleri
        // Artık appsettings.Development.json'daki IdentityOption'dan okunmalı ama şimdilik ServiceApiSettings'e taşıyacağız.
        // Hızlı çözüm için Appsettings'den okuyacak yapıyı kuracağız.

        public IdentityService(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, IOptions<ServiceApiSettings> serviceApiSettings)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _serviceApiSettings = serviceApiSettings.Value;
        }

        public async Task<string?> SignInAsync(SignInViewModel model)
        {
            // 1. Keycloak'a İstek Hazırla
            var tokenEndpoint = $"{_serviceApiSettings.IdentityBaseUri}/protocol/openid-connect/token";

            var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", "web-ui" },
                { "grant_type", "password" },
                { "username", model.Email },
                { "password", model.Password }
            });

            var response = await _httpClient.PostAsync(tokenEndpoint, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                // Hata mesajını loglayabiliriz
                // var errorContent = await response.Content.ReadAsStringAsync();
                return "Kullanıcı adı veya şifre hatalı.";
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (tokenResponse == null)
            {
                return "Token alınamadı.";
            }

            // 2. Token'ı Parse Et ve Cookie Oluştur
            ClaimsPrincipal claimsPrincipal = GetClaimsPrincipal(tokenResponse.AccessToken);
            
            var authenticationProperties = new AuthenticationProperties();
            authenticationProperties.StoreTokens(new List<AuthenticationToken>
            {
                new AuthenticationToken { Name = OpenIdConnectParameterNames.AccessToken, Value = tokenResponse.AccessToken },
                new AuthenticationToken { Name = OpenIdConnectParameterNames.RefreshToken, Value = tokenResponse.RefreshToken },
                new AuthenticationToken { Name = OpenIdConnectParameterNames.ExpiresIn, Value = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString("o", CultureInfo.InvariantCulture) }
            });

            authenticationProperties.IsPersistent = model.IsRemember;

            await _httpContextAccessor.HttpContext!.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authenticationProperties);

            return null; // Başarılı
        }

        public async Task<string?> SignUpAsync(SignUpViewModel model)
        {
            try
            {
                // 1. Admin token al (Keycloak'a kullanıcı eklemek için)
                var adminTokenEndpoint = $"{_serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/realms/master/protocol/openid-connect/token";
                
                var adminUsername = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_USERNAME") ?? "admin";
                var adminPassword = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_PASSWORD") ?? "admin";

                var adminTokenContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", "admin-cli" },
                    { "grant_type", "password" },
                    { "username", adminUsername },
                    { "password", adminPassword }
                });

                var adminTokenResponse = await _httpClient.PostAsync(adminTokenEndpoint, adminTokenContent);
                if (!adminTokenResponse.IsSuccessStatusCode)
                {
                    return "Kayıt işlemi sırasında bir hata oluştu.";
                }

                var adminTokenJson = await adminTokenResponse.Content.ReadAsStringAsync();
                var adminToken = JsonSerializer.Deserialize<TokenResponse>(adminTokenJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (adminToken == null)
                {
                    return "Kayıt işlemi sırasında bir hata oluştu.";
                }

                // 2. Kullanıcıyı oluştur
                var createUserEndpoint = $"{_serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj/users";
                
                var userData = new
                {
                    username = model.Email,
                    email = model.Email,
                    firstName = model.FirstName,
                    lastName = model.LastName,
                    enabled = true,
                    emailVerified = true,
                    credentials = new[]
                    {
                        new
                        {
                            type = "password",
                            value = model.Password,
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

                var createUserResponse = await _httpClient.SendAsync(createUserRequest);
                
                if (!createUserResponse.IsSuccessStatusCode)
                {
                    var errorContent = await createUserResponse.Content.ReadAsStringAsync();
                    if (errorContent.Contains("User exists"))
                    {
                        return "Bu email adresi zaten kullanılıyor.";
                    }
                    return "Kayıt işlemi sırasında bir hata oluştu.";
                }

                return null; // Başarılı
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SignUp Error] {ex.Message}");
                return "Kayıt işlemi sırasında bir hata oluştu.";
            }
        }

        public Task<TokenResponse?> GetAccessTokenByRefreshTokenAsync()
        {
             // Eksik implementasyon: Refresh Token ile yeni Access Token alma
             // Şimdilik null dönelim
             return Task.FromResult<TokenResponse?>(null);
        }

        public async Task RevokeRefreshToken()
        {
            // Eksik implementasyon: Keycloak'tan refresh token'ı iptal etme
            // Şimdilik boş
             await Task.CompletedTask;
        }

        public string GetUserId()
        {
            // 1. Önce "sub" claim'ini dene (JWT standart)
            var userId = _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(userId))
                return userId;

            // 2. ClaimTypes.NameIdentifier'ı dene
            userId = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
                return userId;

            // 3. Alternatif claim türlerini dene
            userId = _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            if (!string.IsNullOrEmpty(userId))
                return userId;

            // 4. Fallback - Bu durumda log yazalım
            Console.WriteLine("[IdentityService] WARNING: UserId not found in claims, returning anonymous-user");
            Console.WriteLine($"[IdentityService] Available claims: {string.Join(", ", _httpContextAccessor.HttpContext?.User.Claims.Select(c => $"{c.Type}={c.Value}") ?? Array.Empty<string>())}");
            
            return "anonymous-user";
        }

        private ClaimsPrincipal GetClaimsPrincipal(string accessToken)
        {
            // JWT Token'ı parse et
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(accessToken);

            var claims = new List<Claim>();
            
            Console.WriteLine("[IdentityService] ========== JWT TOKEN PARSING START ==========");
            Console.WriteLine($"[IdentityService] Token has {jwtToken.Claims.Count()} claims");
            
            // JWT claim'lerini ekle
            foreach (var claim in jwtToken.Claims)
            {
                claims.Add(claim);
                
                // "sub" claim'ini NameIdentifier olarak da ekle
                if (claim.Type == "sub")
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, claim.Value));
                    Console.WriteLine($"[IdentityService] Mapped 'sub' claim to NameIdentifier: {claim.Value}");
                }
                
                // "realm_access" claim'ini Role olarak ekle
                if (claim.Type == "realm_access")
                {
                    Console.WriteLine($"[IdentityService] Found realm_access claim, parsing roles...");
                    
                    try
                    {
                        var rolesJson = JsonSerializer.Deserialize<JsonElement>(claim.Value);
                        if (rolesJson.ValueKind == JsonValueKind.Object && rolesJson.TryGetProperty("roles", out var rolesArray))
                        {
                            Console.WriteLine($"[IdentityService] Found roles array with {rolesArray.GetArrayLength()} roles");
                            foreach (var role in rolesArray.EnumerateArray())
                            {
                                var roleValue = role.GetString();
                                if (!string.IsNullOrEmpty(roleValue))
                                {
                                    claims.Add(new Claim(ClaimTypes.Role, roleValue));
                                    Console.WriteLine($"[IdentityService] ✓ Added role: {roleValue}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[IdentityService] ✗ Error parsing realm_access: {ex.Message}");
                    }
                }
            }

            // Debug: Tüm claim'leri logla
            Console.WriteLine($"[IdentityService] Total claims after processing: {claims.Count}");
            Console.WriteLine($"[IdentityService] Role claims (ClaimTypes.Role):");
            var roleClaims = claims.Where(c => c.Type == ClaimTypes.Role).ToList();
            if (roleClaims.Any())
            {
                foreach (var claim in roleClaims)
                {
                    Console.WriteLine($"  ✓ Role: {claim.Value}");
                }
            }
            else
            {
                Console.WriteLine("  ✗ NO ROLES FOUND!");
            }
            Console.WriteLine("[IdentityService] ========== JWT TOKEN PARSING END ==========");

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, "name", ClaimTypes.Role);
            return new ClaimsPrincipal(claimsIdentity);
        }
    }
}
