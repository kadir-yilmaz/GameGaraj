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
                        ImageUrl = x.PictureUrl ?? ""
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
            public string CategoryId { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public string? PictureUrl { get; set; }
            public int Quantity { get; set; }
        }

        public async Task<bool> SaveOrUpdateAsync(BasketViewModel basket)
        {
            try
            {
                _logger.LogInformation($"[BasketService] Saving basket with {basket.Items.Count} items");
                
                var json = JsonSerializer.Serialize(basket);
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
                    Quantity = item.Quantity
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
    }
}
