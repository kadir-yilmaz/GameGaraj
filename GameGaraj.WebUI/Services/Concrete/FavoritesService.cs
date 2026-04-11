using GameGaraj.WebUI.Services.Abstract;
using System.Text.Json;

namespace GameGaraj.WebUI.Services.Concrete
{
    public class FavoritesService : IFavoritesService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FavoritesService> _logger;

        public FavoritesService(HttpClient httpClient, ILogger<FavoritesService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<string>> GetFavoriteProductIdsAsync()
        {
            try
            {
                _logger.LogInformation("[FavoritesService] Fetching favorites from: {BaseAddress}favorites", _httpClient.BaseAddress);
                
                var response = await _httpClient.GetAsync("favorites");
                
                if (!response.IsSuccessStatusCode)
                    return new List<string>();

                var content = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<FavoritesApiResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return apiResponse?.ProductIds ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FavoritesService] Error fetching favorites");
                return new List<string>();
            }
        }

        public async Task<bool> AddFavoriteAsync(string productId)
        {
            try
            {
                _logger.LogInformation("[FavoritesService] Adding favorite: {ProductId}", productId);
                var response = await _httpClient.PostAsync($"favorites/{productId}", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FavoritesService] Error adding favorite");
                return false;
            }
        }

        public async Task<bool> RemoveFavoriteAsync(string productId)
        {
            try
            {
                _logger.LogInformation("[FavoritesService] Removing favorite: {ProductId}", productId);
                var response = await _httpClient.DeleteAsync($"favorites/{productId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FavoritesService] Error removing favorite");
                return false;
            }
        }

        public async Task<bool> ToggleFavoriteAsync(string productId)
        {
            var favorites = await GetFavoriteProductIdsAsync();
            if (favorites.Contains(productId))
            {
                return await RemoveFavoriteAsync(productId);
            }
            else
            {
                return await AddFavoriteAsync(productId);
            }
        }

        public async Task<bool> IsFavoriteAsync(string productId)
        {
            var favorites = await GetFavoriteProductIdsAsync();
            return favorites.Contains(productId);
        }

        private class FavoritesApiResponse
        {
            public string UserId { get; set; } = string.Empty;
            public List<string> ProductIds { get; set; } = new();
        }
    }
}
