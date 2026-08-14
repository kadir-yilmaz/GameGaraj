using System.Text.Json;
using GameGaraj.WebUI.Models.Common;
using GameGaraj.WebUI.Models.Products;
using GameGaraj.WebUI.Services.Abstract;

namespace GameGaraj.WebUI.Services.Concrete
{
    public class SearchService : ISearchService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SearchService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public SearchService(HttpClient httpClient, ILogger<SearchService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        public async Task<List<ProductViewModel>> SearchProductsAsync(string keyword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyword)) return new List<ProductViewModel>();

                var response = await _httpClient.GetAsync($"?q={Uri.EscapeDataString(keyword)}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Product search failed from Search API: {StatusCode}", response.StatusCode);
                    return new List<ProductViewModel>();
                }

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<ProductViewModel>>(content, _jsonOptions) ?? new List<ProductViewModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products from Search API");
                return new List<ProductViewModel>();
            }
        }

        public async Task<List<SearchSuggestionViewModel>> SearchSuggestionsAsync(string keyword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyword)) return new List<SearchSuggestionViewModel>();

                var response = await _httpClient.GetAsync($"suggestions?q={Uri.EscapeDataString(keyword)}");
                if (!response.IsSuccessStatusCode) return new List<SearchSuggestionViewModel>();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<SearchSuggestionViewModel>>(content, _jsonOptions) ?? new List<SearchSuggestionViewModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching suggestions from Search API");
                return new List<SearchSuggestionViewModel>();
            }
        }

        public async Task<List<string>> SearchBrandsAsync(string keyword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyword)) return new List<string>();

                var response = await _httpClient.GetAsync($"brands?q={Uri.EscapeDataString(keyword)}");
                if (!response.IsSuccessStatusCode) return new List<string>();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<string>>(content, _jsonOptions) ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching brands from Search API");
                return new List<string>();
            }
        }

        public async Task<List<ProductViewModel>> GetFeaturedProductsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("products/featured");
                if (!response.IsSuccessStatusCode) return new List<ProductViewModel>();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<ProductViewModel>>(content, _jsonOptions) ?? new List<ProductViewModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching featured products from Search API");
                return new List<ProductViewModel>();
            }
        }

        public async Task<ProductViewModel?> GetProductByIdAsync(string id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"products/{id}");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ProductViewModel>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching product by ID from Search API");
                return null;
            }
        }

        public async Task<ProductViewModel?> GetProductBySlugAsync(string slug)
        {
            try
            {
                var response = await _httpClient.GetAsync($"products/slug/{slug}");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ProductViewModel>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching product by Slug from Search API");
                return null;
            }
        }

        public async Task<List<ProductViewModel>> GetFilteredProductsAsync(
            string? categoryId = null,
            string? sortBy = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            Dictionary<string, string[]>? specs = null,
            string? brand = null)
        {
            try
            {
                var queryParams = new List<string>();
                if (!string.IsNullOrWhiteSpace(categoryId)) queryParams.Add($"categoryId={Uri.EscapeDataString(categoryId)}");
                if (!string.IsNullOrWhiteSpace(sortBy)) queryParams.Add($"sortBy={Uri.EscapeDataString(sortBy)}");
                if (minPrice.HasValue) queryParams.Add($"minPrice={minPrice.Value}");
                if (maxPrice.HasValue) queryParams.Add($"maxPrice={maxPrice.Value}");
                if (!string.IsNullOrWhiteSpace(brand)) queryParams.Add($"brand={Uri.EscapeDataString(brand)}");

                if (specs != null)
                {
                    foreach (var spec in specs)
                    {
                        if (spec.Value != null && spec.Value.Length > 0)
                        {
                            var values = string.Join(",", spec.Value);
                            queryParams.Add($"{Uri.EscapeDataString(spec.Key)}={Uri.EscapeDataString(values)}");
                        }
                    }
                }

                var url = "products" + (queryParams.Any() ? "?" + string.Join("&", queryParams) : "");
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<ProductViewModel>();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<ProductViewModel>>(content, _jsonOptions) ?? new List<ProductViewModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching filtered products from Search API");
                return new List<ProductViewModel>();
            }
        }

        public async Task<SearchIndexStatusViewModel?> GetSearchIndexStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("status");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<SearchIndexStatusViewModel>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching index status from Search API");
                return null;
            }
        }

        public async Task<SearchIndexDocumentPageViewModel> GetSearchIndexDocumentPreviewsAsync(int page = 1, int pageSize = 100)
        {
            try
            {
                var response = await _httpClient.GetAsync($"documents?page={page}&pageSize={pageSize}");
                if (!response.IsSuccessStatusCode) return new SearchIndexDocumentPageViewModel();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<SearchIndexDocumentPageViewModel>(content, _jsonOptions) ?? new SearchIndexDocumentPageViewModel();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching document previews from Search API");
                return new SearchIndexDocumentPageViewModel();
            }
        }

        public async Task<ReindexResultViewModel?> ReindexSearchIndexAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("reindex", null);
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ReindexResultViewModel>(content, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error triggering reindex in Search API");
                return null;
            }
        }
    }
}
