using Microsoft.AspNetCore.Http;
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

        public async Task<(string? Error, string? UserId)> SignInAsync(SignInViewModel model)
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
                return ("Kullanıcı adı veya şifre hatalı.", null);
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (tokenResponse == null)
            {
                return ("Token alınamadı.", null);
            }

            // 2. Token'ı Parse Et ve Cookie Oluştur
            ClaimsPrincipal claimsPrincipal = GetClaimsPrincipal(tokenResponse.AccessToken);
            
            var userId = claimsPrincipal.FindFirst("sub")?.Value 
                         ?? claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var authenticationProperties = new AuthenticationProperties();
            authenticationProperties.StoreTokens(new List<AuthenticationToken>
            {
                new AuthenticationToken { Name = OpenIdConnectParameterNames.AccessToken, Value = tokenResponse.AccessToken },
                new AuthenticationToken { Name = OpenIdConnectParameterNames.RefreshToken, Value = tokenResponse.RefreshToken },
                new AuthenticationToken { Name = "expires_at", Value = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString("o", System.Globalization.CultureInfo.InvariantCulture) }
            });

            authenticationProperties.IsPersistent = model.IsRemember;

            await _httpContextAccessor.HttpContext!.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal, authenticationProperties);

            return (null, userId); // Başarılı
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

        public async Task<TokenResponse?> GetAccessTokenByRefreshTokenAsync()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            var refreshToken = await httpContext.GetTokenAsync(OpenIdConnectParameterNames.RefreshToken);
            if (string.IsNullOrEmpty(refreshToken)) return null;

            var tokenEndpoint = $"{_serviceApiSettings.IdentityBaseUri}/protocol/openid-connect/token";

            var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", "web-ui" },
                { "grant_type", "refresh_token" },
                { "refresh_token", refreshToken }
            });

            try
            {
                var response = await _httpClient.PostAsync(tokenEndpoint, requestContent);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                return tokenResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IdentityService] Refresh token error: {ex.Message}");
                return null;
            }
        }

        public async Task RevokeRefreshToken()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            var refreshToken = await httpContext.GetTokenAsync(OpenIdConnectParameterNames.RefreshToken);
            if (string.IsNullOrEmpty(refreshToken)) return;

            try
            {
                var logoutEndpoint = $"{_serviceApiSettings.IdentityBaseUri}/protocol/openid-connect/logout";

                var requestContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", "web-ui" },
                    { "refresh_token", refreshToken }
                });

                var response = await _httpClient.PostAsync(logoutEndpoint, requestContent);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[IdentityService] Failed to revoke refresh token: {errorContent}");
                }
                else
                {
                    Console.WriteLine("[IdentityService] Keycloak backchannel logout successful.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IdentityService] Error revoking refresh token: {ex.Message}");
            }
        }

        public string GetUserId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return "anonymous-user";

            // 1. Önce "sub" claim'ini dene (JWT standart)
            var userId = httpContext.User.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(userId))
                return userId;

            // 2. ClaimTypes.NameIdentifier'ı dene
            userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
                return userId;

            // 3. Alternatif claim türlerini dene
            userId = httpContext.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            if (!string.IsNullOrEmpty(userId))
                return userId;

            // 4. Misafir (Guest) Kullanıcı için benzersiz Cookie ID oluştur/oku
            var guestCookieName = "GameGarajGuestId";
            if (httpContext.Request.Cookies.TryGetValue(guestCookieName, out var guestId) && !string.IsNullOrEmpty(guestId))
            {
                return guestId;
            }

            // Yoksa yeni oluştur ve cookie'ye kaydet
            var newGuestId = $"guest-{Guid.NewGuid():N}";
            if (!httpContext.Response.HasStarted)
            {
                var cookieOptions = new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddDays(30),
                    HttpOnly = true,
                    IsEssential = true,
                    Secure = httpContext.Request.IsHttps
                };
                httpContext.Response.Cookies.Append(guestCookieName, newGuestId, cookieOptions);
            }
            return newGuestId;
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

        public async Task<List<UserSearchViewModel>> SearchUsersAsync(string query)
        {
            try
            {
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
                    return new();
                }

                var adminTokenJson = await adminTokenResponse.Content.ReadAsStringAsync();
                var adminToken = JsonSerializer.Deserialize<TokenResponse>(adminTokenJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (adminToken == null)
                {
                    return new();
                }

                var searchUsersEndpoint = $"{_serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj/users?search={Uri.EscapeDataString(query)}&max=20";
                
                var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUsersEndpoint);
                searchRequest.Headers.Add("Authorization", $"Bearer {adminToken.AccessToken}");

                var searchResponse = await _httpClient.SendAsync(searchRequest);
                if (!searchResponse.IsSuccessStatusCode)
                {
                    return new();
                }

                var searchContent = await searchResponse.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<UserSearchViewModel>>(searchContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return users ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IdentityService SearchUsersAsync Error] {ex.Message}");
                return new();
            }
        }
    }
}
