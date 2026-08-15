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

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace GameGaraj.WebUI.Services.Concrete
{
    public class IdentityService : IIdentityService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ServiceApiSettings _serviceApiSettings;
        private readonly IDistributedCache _distributedCache;
        private readonly IConfiguration _configuration;

        public IdentityService(
            HttpClient httpClient, 
            IHttpContextAccessor httpContextAccessor, 
            IOptions<ServiceApiSettings> serviceApiSettings,
            IDistributedCache distributedCache,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _serviceApiSettings = serviceApiSettings.Value;
            _distributedCache = distributedCache;
            _configuration = configuration;
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

            authenticationProperties.IsPersistent = true;
            authenticationProperties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7);

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

        public async Task<TokenResponse?> GetAccessTokenByRefreshTokenAsync(string? refreshToken = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            if (string.IsNullOrEmpty(refreshToken) && httpContext != null)
            {
                refreshToken = await httpContext.GetTokenAsync(OpenIdConnectParameterNames.RefreshToken);
            }

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

        private async Task<string?> GetAdminAccessTokenAsync()
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
                if (!adminTokenResponse.IsSuccessStatusCode) return null;

                var adminTokenJson = await adminTokenResponse.Content.ReadAsStringAsync();
                var adminToken = JsonSerializer.Deserialize<TokenResponse>(adminTokenJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return adminToken?.AccessToken;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<UserSearchViewModel>> SearchUsersAsync(string query)
        {
            try
            {
                var accessToken = await GetAdminAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken)) return new();

                var searchUsersEndpoint = $"{_serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj/users?search={Uri.EscapeDataString(query)}&max=20";
                
                var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchUsersEndpoint);
                searchRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

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

        public async Task<(bool Success, string? Message)> SendPasswordResetEmailAsync(string emailOrUsername)
        {
            try
            {
                var accessToken = await GetAdminAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                    return (false, "Admin yetkisi alınamadı.");

                var baseAdminUrl = $"{_serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj";
                
                var searchEndpoint = $"{baseAdminUrl}/users?email={Uri.EscapeDataString(emailOrUsername)}&exact=true";
                var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchEndpoint);
                searchRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
                var searchResponse = await _httpClient.SendAsync(searchRequest);

                List<UserSearchViewModel>? users = null;
                if (searchResponse.IsSuccessStatusCode)
                {
                    var content = await searchResponse.Content.ReadAsStringAsync();
                    users = JsonSerializer.Deserialize<List<UserSearchViewModel>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }

                if (users == null || !users.Any())
                {
                    var searchUserEndpoint = $"{baseAdminUrl}/users?username={Uri.EscapeDataString(emailOrUsername)}&exact=true";
                    var searchUserReq = new HttpRequestMessage(HttpMethod.Get, searchUserEndpoint);
                    searchUserReq.Headers.Add("Authorization", $"Bearer {accessToken}");
                    var searchUserResp = await _httpClient.SendAsync(searchUserReq);
                    if (searchUserResp.IsSuccessStatusCode)
                    {
                        var content = await searchUserResp.Content.ReadAsStringAsync();
                        users = JsonSerializer.Deserialize<List<UserSearchViewModel>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                }

                if (users == null || !users.Any())
                {
                    return (false, "Kullanıcı bulunamadı.");
                }

                var userId = users.First().Id;

                var actionEndpoint = $"{baseAdminUrl}/users/{userId}/execute-actions-email";
                var actionRequest = new HttpRequestMessage(HttpMethod.Put, actionEndpoint);
                actionRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
                actionRequest.Content = new StringContent(
                    JsonSerializer.Serialize(new[] { "UPDATE_PASSWORD" }),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var actionResponse = await _httpClient.SendAsync(actionRequest);
                if (actionResponse.IsSuccessStatusCode)
                {
                    return (true, "Şifre sıfırlama e-postası başarıyla gönderildi.");
                }

                var errorBody = await actionResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"[SendPasswordResetEmailAsync Keycloak Error] StatusCode: {(int)actionResponse.StatusCode}, Body: {errorBody}");
                return (false, "Şifre sıfırlama e-postası gönderilirken bir hata oluştu.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendPasswordResetEmailAsync Error] {ex.Message}");
                return (false, "İşlem sırasında bir hata meydana geldi.");
            }
        }

        public async Task<(bool Success, string? Message)> ChangePasswordAsync(string userId, string newPassword)
        {
            try
            {
                var accessToken = await GetAdminAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                    return (false, "Admin yetkisi alınamadı.");

                var baseAdminUrl = $"{_serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj";
                var resetEndpoint = $"{baseAdminUrl}/users/{userId}/reset-password";

                var resetRequest = new HttpRequestMessage(HttpMethod.Put, resetEndpoint);
                resetRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
                resetRequest.Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        type = "password",
                        value = newPassword,
                        temporary = false
                    }),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var resetResponse = await _httpClient.SendAsync(resetRequest);
                if (resetResponse.IsSuccessStatusCode)
                {
                    return (true, "Şifreniz başarıyla güncellendi.");
                }

                return (false, "Şifre güncellenirken bir hata oluştu.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChangePasswordAsync Error] {ex.Message}");
                return (false, "İşlem sırasında bir hata meydana geldi.");
            }
        }

        public async Task<(bool Success, string? Message)> SendPasswordResetOtpAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                    return (false, "Lütfen geçerli bir e-posta adresi giriniz.");

                email = email.Trim().ToLowerInvariant();

                // 1. Check if user exists in Keycloak
                var accessToken = await GetAdminAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                    return (false, "Kimlik doğrulama servisine ulaşılamadı.");

                var baseAdminUrl = $"{_serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj";
                var searchEndpoint = $"{baseAdminUrl}/users?email={Uri.EscapeDataString(email)}&exact=true";

                var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchEndpoint);
                searchRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

                var searchResponse = await _httpClient.SendAsync(searchRequest);
                if (!searchResponse.IsSuccessStatusCode)
                {
                    return (false, "Kullanıcı sorgulanırken bir hata oluştu.");
                }

                var usersJson = await searchResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(usersJson);
                var root = doc.RootElement;

                if (root.GetArrayLength() == 0)
                {
                    return (false, "Bu e-posta adresiyle kayıtlı bir hesap bulunamadı.");
                }

                var userElem = root[0];
                string firstName = userElem.TryGetProperty("firstName", out var fn) ? fn.GetString() ?? "Kullanıcı" : "Kullanıcı";

                // 2. Generate 6-digit OTP code
                string otpCode = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

                // 3. Save OTP in cache (10 minutes expiry)
                string cacheKey = $"pwd_reset_otp_{email}";
                await _distributedCache.SetStringAsync(cacheKey, otpCode, new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });

                // 4. Send Branded Email via SMTP
                await SendOtpEmailAsync(email, firstName, otpCode);

                return (true, "Doğrulama kodu e-posta adresinize gönderildi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SendPasswordResetOtpAsync Error] {ex.Message}");
                return (false, "Doğrulama kodu gönderilirken bir hata oluştu: " + ex.Message);
            }
        }

        public async Task<(bool Success, string? Message)> ResetPasswordWithOtpAsync(string email, string otpCode, string newPassword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode) || string.IsNullOrWhiteSpace(newPassword))
                    return (false, "Tüm alanları doldurunuz.");

                email = email.Trim().ToLowerInvariant();
                otpCode = otpCode.Trim();

                // 1. Verify OTP from Cache
                string cacheKey = $"pwd_reset_otp_{email}";
                string? cachedOtp = await _distributedCache.GetStringAsync(cacheKey);

                if (string.IsNullOrEmpty(cachedOtp) || cachedOtp != otpCode)
                {
                    return (false, "Girdiğiniz 6 haneli doğrulama kodu geçersiz veya süresi dolmuş. Lütfen tekrar deneyiniz.");
                }

                // 2. Get Keycloak User Id
                var accessToken = await GetAdminAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                    return (false, "Admin yetkisi alınamadı.");

                var baseAdminUrl = $"{_serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj";
                var searchEndpoint = $"{baseAdminUrl}/users?email={Uri.EscapeDataString(email)}&exact=true";

                var searchRequest = new HttpRequestMessage(HttpMethod.Get, searchEndpoint);
                searchRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

                var searchResponse = await _httpClient.SendAsync(searchRequest);
                if (!searchResponse.IsSuccessStatusCode)
                {
                    return (false, "Kullanıcı bilgisi alınamadı.");
                }

                var usersJson = await searchResponse.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(usersJson);
                var root = doc.RootElement;

                if (root.GetArrayLength() == 0)
                {
                    return (false, "Kullanıcı bulunamadı.");
                }

                string userId = root[0].GetProperty("id").GetString()!;

                // 3. Reset Password in Keycloak
                var changeResult = await ChangePasswordAsync(userId, newPassword);
                if (!changeResult.Success)
                {
                    return changeResult;
                }

                // 4. Invalidate OTP from cache
                await _distributedCache.RemoveAsync(cacheKey);

                return (true, "Şifreniz başarıyla sıfırlandı. Giriş yapabilirsiniz.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ResetPasswordWithOtpAsync Error] {ex.Message}");
                return (false, "Şifre sıfırlama işlemi sırasında bir hata meydana geldi: " + ex.Message);
            }
        }

        private async Task SendOtpEmailAsync(string toEmail, string name, string otpCode)
        {
            string host = _configuration["SMTP_HOST"] ?? "smtp.gmail.com";
            int port = int.TryParse(_configuration["SMTP_PORT"], out int p) ? p : 587;
            string user = _configuration["SMTP_USER"] ?? "kadiryilmaz19821@gmail.com";
            string pass = _configuration["SMTP_PASSWORD"] ?? "dtbfbkverbcvrnok";
            string from = _configuration["SMTP_FROM_EMAIL"] ?? "kadiryilmaz19821@gmail.com";
            string fromName = _configuration["SMTP_FROM_NAME"] ?? "GameGaraj";

            using var client = new System.Net.Mail.SmtpClient(host, port)
            {
                Credentials = new System.Net.NetworkCredential(user, pass),
                EnableSsl = true
            };

            var mail = new System.Net.Mail.MailMessage
            {
                From = new System.Net.Mail.MailAddress(from, fromName),
                Subject = $"🎮 GameGaraj - Doğrulama Kodunuz: {otpCode}",
                IsBodyHtml = true
            };
            mail.To.Add(toEmail);

            mail.Body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #0b1120; color: #f8fafc; margin: 0; padding: 40px 20px; }}
        .container {{ max-width: 520px; margin: 0 auto; background: #131b2e; border: 1px solid rgba(255,255,255,0.1); border-radius: 16px; padding: 36px 30px; box-shadow: 0 20px 40px rgba(0,0,0,0.5); }}
        .logo {{ text-align: center; margin-bottom: 24px; font-size: 26px; font-weight: 800; color: #ff6b00; letter-spacing: 1px; }}
        .title {{ font-size: 20px; font-weight: 700; color: #ffffff; text-align: center; margin-bottom: 12px; }}
        .text {{ font-size: 14px; color: #94a3b8; line-height: 1.6; text-align: center; margin-bottom: 28px; }}
        .otp-box {{ background: #0b1120; border: 2px dashed #ff6b00; border-radius: 12px; padding: 18px 24px; text-align: center; margin-bottom: 28px; }}
        .otp-code {{ font-size: 36px; font-weight: 900; letter-spacing: 8px; color: #ff6b00; font-family: 'Courier New', monospace; }}
        .expire-note {{ font-size: 12px; color: #64748b; text-align: center; margin-top: 10px; }}
        .divider {{ border-top: 1px solid rgba(255,255,255,0.08); margin: 24px 0; }}
        .footer {{ font-size: 11px; color: #64748b; text-align: center; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='logo'>🎮 GAMEGARAJ</div>
        <div class='title'>Şifre Sıfırlama Doğrulama Kodu</div>
        <div class='text'>
            Merhaba <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,<br>
            GameGaraj hesabınız için şifre sıfırlama talebinde bulundunuz. Aşağıdaki 6 haneli doğrulama kodunu sitemizdeki ekrana girerek yeni şifrenizi belirleyebilirsiniz.
        </div>
        <div class='otp-box'>
            <div class='otp-code'>{otpCode}</div>
            <div class='expire-note'>⏳ Bu kod <strong>10 dakika</strong> boyunca geçerlidir.</div>
        </div>
        <div class='text' style='font-size: 12px; margin-bottom: 0;'>
            Bu talebi siz yapmadıysanız bu e-postayı güvenle silebilirsiniz. Şifreniz değişmeyecektir.
        </div>
        <div class='divider'></div>
        <div class='footer'>
            © {DateTime.Now.Year} GameGaraj - Performansın Adresi<br>
            Bu otomatik bir e-postadır, lütfen doğrudan yanıtlamayınız.
        </div>
    </div>
</body>
</html>";

            await client.SendMailAsync(mail);
        }
    }
}
