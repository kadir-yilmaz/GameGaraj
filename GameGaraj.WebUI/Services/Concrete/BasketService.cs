using GameGaraj.WebUI.Models.Baskets;
using GameGaraj.WebUI.Services.Abstract;
using System.Text;
using System.Text.Json;

namespace GameGaraj.WebUI.Services.Concrete
{
    public class BasketService : IBasketService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BasketService> _logger;
        private readonly IIdentityService _identityService;

        public BasketService(
            HttpClient httpClient, 
            ILogger<BasketService> logger,
            IIdentityService identityService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _identityService = identityService;
        }

        public async Task<BasketViewModel?> GetBasketAsync()
        {
            try
            {
                _logger.LogInformation($"[BasketService] Fetching basket from: {_httpClient.BaseAddress}baskets");
                
                var response = await _httpClient.GetAsync("baskets");
                
                _logger.LogInformation($"[BasketService] Response Status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                    return null;

                var content = await response.Content.ReadAsStringAsync();
                
                // Basket API'den gelen format: { userId, items: [{ id, name, price, pictureUrl, quantity }] }
                var apiResponse = JsonSerializer.Deserialize<BasketApiResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse == null)
                    return null;

                // WebUI formatına dönüştür
                var basket = new BasketViewModel
                {
                    UserId = apiResponse.UserId,
                    Items = apiResponse.Items.Select(x => new BasketItemViewModel
                    {
                        ProductId = x.Id,
                        ProductName = x.Name, // Name -> ProductName mapping
                        CategoryId = x.CategoryId ?? string.Empty,
                        Price = x.Price,
                        Quantity = x.Quantity,
                        ImageUrl = x.PictureUrl ?? "",
                        ProductSlug = x.ProductSlug ?? "",
                        Brand = x.Brand ?? string.Empty
                    }).ToList()
                };

                return basket;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BasketService] Error fetching basket");
                return null;
            }
        }

        // Basket API response modeli
        private class BasketApiResponse
        {
            public string UserId { get; set; } = string.Empty;
            public List<BasketApiItem> Items { get; set; } = new();
        }

        private class BasketApiItem
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string ProductSlug { get; set; } = string.Empty;
            public string CategoryId { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public string? PictureUrl { get; set; }
            public int Quantity { get; set; }
            public string? Brand { get; set; }
        }

        public async Task<bool> SaveOrUpdateAsync(BasketViewModel basket)
        {
            try
            {
                _logger.LogInformation($"[BasketService] Saving basket with {basket.Items.Count} items");
                
                var payload = new
                {
                    UserId = basket.UserId,
                    Items = basket.Items.Select(x => new
                    {
                        Id = x.ProductId,
                        Name = x.ProductName,
                        CategoryId = x.CategoryId,
                        Price = x.Price,
                        PictureUrl = x.ImageUrl,
                        Quantity = x.Quantity,
                        ProductSlug = x.ProductSlug,
                        Brand = x.Brand
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(payload);
                var stringContent = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync("baskets", stringContent);
                
                _logger.LogInformation($"[BasketService] Save Response Status: {response.StatusCode}");
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BasketService] Error saving basket");
                return false;
            }
        }

        public async Task<bool> DeleteAsync()
        {
            var response = await _httpClient.DeleteAsync("baskets");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AddItemAsync(BasketItemViewModel item)
        {
            try
            {
                _logger.LogInformation($"[BasketService] Adding item: {item.ProductName} (ID: {item.ProductId})");
                
                // Basket API'nin beklediği format
                var basketItem = new
                {
                    Id = item.ProductId,
                    Name = item.ProductName,
                    CategoryId = item.CategoryId,
                    Price = item.Price,
                    PictureUrl = item.ImageUrl,
                    ProductSlug = item.ProductSlug,
                    Quantity = item.Quantity,
                    Brand = item.Brand
                };

                var json = JsonSerializer.Serialize(basketItem);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("baskets/items", content);
                
                _logger.LogInformation($"[BasketService] Add item response: {response.StatusCode}");
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BasketService] Error adding item");
                return false;
            }
        }

        public async Task<bool> RemoveItemAsync(string productId)
        {
            var response = await _httpClient.DeleteAsync($"baskets/items/{productId}");
            return response.IsSuccessStatusCode;
        }

        public async Task SyncBasketAsync(string guestId, string userId)
        {
            try
            {
                _logger.LogInformation($"[BasketService] Syncing guest basket ({guestId}) to user basket ({userId})");

                // 1. Misafir sepetini guestId header'ı ile çek
                var guestRequest = new HttpRequestMessage(HttpMethod.Get, "baskets");
                guestRequest.Headers.Add("X-User-Id", guestId);
                var guestResponse = await _httpClient.SendAsync(guestRequest);
                if (!guestResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"[BasketService] No guest basket or failed to fetch: {guestResponse.StatusCode}");
                    return;
                }

                var guestContent = await guestResponse.Content.ReadAsStringAsync();
                var guestApiResponse = JsonSerializer.Deserialize<BasketApiResponse>(guestContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (guestApiResponse == null || guestApiResponse.Items == null || !guestApiResponse.Items.Any())
                {
                    _logger.LogInformation("[BasketService] Guest basket is empty. Nothing to sync.");
                    return; // Birleştirilecek ürün yok
                }

                // 2. Üye sepetini userId header'ı ile çek (varsa)
                var userRequest = new HttpRequestMessage(HttpMethod.Get, "baskets");
                userRequest.Headers.Add("X-User-Id", userId);
                var userResponse = await _httpClient.SendAsync(userRequest);
                
                List<BasketApiItem> mergedItems = new();
                if (userResponse.IsSuccessStatusCode)
                {
                    var userContent = await userResponse.Content.ReadAsStringAsync();
                    var userApiResponse = JsonSerializer.Deserialize<BasketApiResponse>(userContent, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    if (userApiResponse != null && userApiResponse.Items != null)
                    {
                        mergedItems.AddRange(userApiResponse.Items);
                    }
                }

                // 3. Misafir sepetindeki ürünleri üye sepetindekilerle birleştir
                foreach (var guestItem in guestApiResponse.Items)
                {
                    var existingItem = mergedItems.FirstOrDefault(x => x.Id == guestItem.Id);
                    if (existingItem != null)
                    {
                        existingItem.Quantity += guestItem.Quantity;
                    }
                    else
                    {
                        mergedItems.Add(guestItem);
                    }
                }

                // 4. Birleştirilmiş sepeti üye ID'si ile kaydet
                var saveBody = new
                {
                    UserId = userId,
                    Items = mergedItems.Select(x => new
                    {
                        Id = x.Id,
                        Name = x.Name,
                        CategoryId = x.CategoryId,
                        Price = x.Price,
                        PictureUrl = x.PictureUrl,
                        ProductSlug = x.ProductSlug,
                        Quantity = x.Quantity,
                        Brand = x.Brand
                    }).ToList()
                };

                var saveRequest = new HttpRequestMessage(HttpMethod.Post, "baskets");
                saveRequest.Headers.Add("X-User-Id", userId);
                saveRequest.Content = new StringContent(JsonSerializer.Serialize(saveBody), Encoding.UTF8, "application/json");
                
                var saveResponse = await _httpClient.SendAsync(saveRequest);
                if (!saveResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"[BasketService] Failed to save merged basket: {saveResponse.StatusCode}");
                    return;
                }

                // 5. Misafir sepetini temizle
                var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "baskets");
                deleteRequest.Headers.Add("X-User-Id", guestId);
                var deleteResponse = await _httpClient.SendAsync(deleteRequest);
                if (deleteResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"[BasketService] Successfully synced and cleared guest basket ({guestId}) for user ({userId}).");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BasketService] Error syncing basket");
            }
        }
    }
}
