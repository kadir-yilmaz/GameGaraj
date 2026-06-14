using GameGaraj.WebUI.Models.Auth;
using GameGaraj.WebUI.Settings;
using System.Text.Json;

namespace GameGaraj.WebUI.Seeds
{
    public static class KeycloakSeeder
    {
        public static async Task SeedAdminUserAsync(this WebApplication app)
        {
            var configuration = app.Configuration;
            
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

                using var scope = app.Services.CreateScope();
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
                
                var keycloakAdminUsername = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_USERNAME") ?? "admin";
                var keycloakAdminPassword = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_PASSWORD") ?? "admin";

                var adminTokenContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "client_id", "admin-cli" },
                    { "grant_type", "password" },
                    { "username", keycloakAdminUsername },
                    { "password", keycloakAdminPassword }
                });

                var adminTokenResponse = await httpClient.PostAsync(adminTokenEndpoint, adminTokenContent);
                if (!adminTokenResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("[Seed-Admin] ✗ Failed to get admin token from Keycloak");
                    return;
                }

                var adminTokenJson = await adminTokenResponse.Content.ReadAsStringAsync();
                var adminToken = JsonSerializer.Deserialize<TokenResponse>(adminTokenJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (adminToken == null || string.IsNullOrEmpty(adminToken.AccessToken))
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
                
                Console.WriteLine($"[Seed-Admin] GetUsers URL: {getUsersEndpoint}");
                Console.WriteLine($"[Seed-Admin] GetUsers Response Status: {getUsersResponse.StatusCode}");
                
                if (!getUsersResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Seed-Admin] ✗ GetUsers API error: {getUsersJson}");
                    return;
                }

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

                // 4. Admin rolünü Keycloak'tan sorgula (Gerçek ID'sini almak için)
                var getRoleEndpoint = $"{serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj/roles/admin";
                var getRoleRequest = new HttpRequestMessage(HttpMethod.Get, getRoleEndpoint);
                getRoleRequest.Headers.Add("Authorization", $"Bearer {adminToken.AccessToken}");
                
                var getRoleResponse = await httpClient.SendAsync(getRoleRequest);
                if (!getRoleResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine("[Seed-Admin] ✗ Failed to get 'admin' role details from Keycloak");
                    return;
                }
                
                var getRoleJson = await getRoleResponse.Content.ReadAsStringAsync();
                var roleObj = JsonSerializer.Deserialize<JsonElement>(getRoleJson);
                var actualRoleId = roleObj.GetProperty("id").GetString();

                // 5. Yeni oluşturulan kullanıcının ID'sini al
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

                // 6. Admin rolünü kullanıcıya ata
                var assignRoleEndpoint = $"{serviceApiSettings.IdentityBaseUri.Replace("/realms/GameGaraj", "")}/admin/realms/GameGaraj/users/{userId}/role-mappings/realm";
                
                var roleData = new[]
                {
                    new
                    {
                        id = actualRoleId,
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
    }
}
