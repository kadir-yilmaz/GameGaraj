using GameGaraj.WebUI.Models.Baskets;
using GameGaraj.WebUI.Models.Products;
using GameGaraj.WebUI.Services.Abstract;
using GameGaraj.WebUI.Settings;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GameGaraj.WebUI.Services.Concrete
{
    public class CatalogService : ICatalogService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CatalogService> _logger;
        private readonly ServiceApiSettings _settings; // Added

        public CatalogService(HttpClient httpClient, ILogger<CatalogService> logger, IOptions<ServiceApiSettings> settings) // Modified constructor
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = settings.Value; // Initialized settings
        }

        private static string NormalizeSearchText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var normalized = value.Trim().ToLower(new CultureInfo("tr-TR")).Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString()
                .Replace('ı', 'i')
                .Replace('ğ', 'g')
                .Replace('ü', 'u')
                .Replace('ş', 's')
                .Replace('ö', 'o')
                .Replace('ç', 'c');
        }

        private static int GetEditDistance(string left, string right)
        {
            if (left == right) return 0;
            if (left.Length == 0) return right.Length;
            if (right.Length == 0) return left.Length;

            var costs = new int[right.Length + 1];
            for (var j = 0; j <= right.Length; j++) costs[j] = j;

            for (var i = 1; i <= left.Length; i++)
            {
                var previous = costs[0];
                costs[0] = i;

                for (var j = 1; j <= right.Length; j++)
                {
                    var current = costs[j];
                    costs[j] = left[i - 1] == right[j - 1]
                        ? previous
                        : Math.Min(Math.Min(costs[j] + 1, costs[j - 1] + 1), previous + 1);
                    previous = current;
                }
            }

            return costs[right.Length];
        }

        private static int GetSearchScore(string text, string keyword)
        {
            var normalizedText = NormalizeSearchText(text);
            var normalizedKeyword = NormalizeSearchText(keyword);
            if (string.IsNullOrWhiteSpace(normalizedText) || string.IsNullOrWhiteSpace(normalizedKeyword)) return int.MaxValue;

            if (normalizedText == normalizedKeyword) return 0;
            if (normalizedText.StartsWith(normalizedKeyword)) return 1;
            if (normalizedText.Contains(normalizedKeyword)) return 2;

            var distance = GetEditDistance(normalizedText, normalizedKeyword);
            var allowedDistance = normalizedKeyword.Length <= 5 ? 1 : 2;
            return distance <= allowedDistance ? 10 + distance : int.MaxValue;
        }

        private void SetProductImageUrls(List<ProductViewModel>? products)
        {
            if (products == null || !products.Any()) return;
            foreach (var product in products)
            {
                SetProductImageUrls(product);
            }
        }

        private void SetProductImageUrls(ProductViewModel? product)
        {
            if (product?.ImageUrls == null || !product.ImageUrls.Any()) return;

            for (int i = 0; i < product.ImageUrls.Count; i++)
            {
                if (!product.ImageUrls[i].StartsWith("http"))
                {
                    // Ensure setting has trailing slash or relative path doesn't start with slash
                    var imgBase = !string.IsNullOrEmpty(_settings.PhotoBaseUrl) ? _settings.PhotoBaseUrl : _settings.PhotoStockUri;
                    var baseUrl = imgBase.EndsWith("/") ? imgBase : imgBase + "/";
                    var path = product.ImageUrls[i].StartsWith("/") ? product.ImageUrls[i].Substring(1) : product.ImageUrls[i];
                    product.ImageUrls[i] = baseUrl + path;
                }
            }
        }

        public async Task<List<ProductViewModel>> GetAllProductsAsync(string? categoryId = null, string? sortBy = null, decimal? minPrice = null, decimal? maxPrice = null, Dictionary<string, string[]>? specs = null, string? brand = null)
        {
            try
            {
                // Build query parameters
                var queryBuilder = new StringBuilder("products");
                var queryParams = new List<string>();

                if (!string.IsNullOrEmpty(categoryId))
                    queryParams.Add($"categoryId={categoryId}");

                if (!string.IsNullOrEmpty(sortBy))
                    queryParams.Add($"sortBy={sortBy}");

                if (minPrice.HasValue)
                    queryParams.Add($"minPrice={minPrice}");

                if (maxPrice.HasValue)
                    queryParams.Add($"maxPrice={maxPrice}");

                if (!string.IsNullOrWhiteSpace(brand))
                    queryParams.Add($"brand={Uri.EscapeDataString(brand)}");

                if (specs != null && specs.Any())
                {
                    var reservedSpecKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "category", "categoryId", "sortBy", "minPrice", "maxPrice", "search", "brand"
                    };

                    foreach (var spec in specs)
                    {
                        if (reservedSpecKeys.Contains(spec.Key)) continue;

                        if (spec.Value != null && spec.Value.Length > 0)
                        {
                            var values = string.Join(",", spec.Value.Where(v => !string.IsNullOrEmpty(v)));
                            if (!string.IsNullOrEmpty(values))
                            {
                                queryParams.Add($"specs[{spec.Key}]={Uri.EscapeDataString(values)}");
                            }
                        }
                    }
                }

                if (queryParams.Any())
                    queryBuilder.Append("?").Append(string.Join("&", queryParams));

                var requestUri = queryBuilder.ToString();
                _logger.LogInformation($"[CatalogService] Fetching products with: {requestUri}");

                var response = await _httpClient.GetAsync(requestUri);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"[CatalogService] Failed to fetch products. Status: {response.StatusCode}");
                    return new List<ProductViewModel>();
                }

                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"[CatalogService] Response length: {content.Length}");

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                List<ProductViewModel>? products = null;

                // Robust Deserialization: Handle both raw arrays and wrapped objects (value: [])
                if (!string.IsNullOrWhiteSpace(content))
                {
                    if (content.TrimStart().StartsWith("{"))
                    {
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("value", out var valueProp))
                        {
                            products = JsonSerializer.Deserialize<List<ProductViewModel>>(valueProp.GetRawText(), options);
                        }
                    }

                    if (products == null)
                    {
                        products = JsonSerializer.Deserialize<List<ProductViewModel>>(content, options);
                    }
                }

                var result = products ?? new List<ProductViewModel>();
                SetProductImageUrls(result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CatalogService] Error fetching products");
                return new List<ProductViewModel>();
            }
        }

        public async Task<List<ProductViewModel>> SearchProductsAsync(string keyword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyword)) return new List<ProductViewModel>();
                
                var response = await _httpClient.GetAsync($"products/search?q={Uri.EscapeDataString(keyword)}");

                if (!response.IsSuccessStatusCode)
                    return new List<ProductViewModel>();

                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                List<ProductViewModel>? products = null;

                if (!string.IsNullOrWhiteSpace(content))
                {
                    if (content.TrimStart().StartsWith("{"))
                    {
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("value", out var valueProp))
                        {
                            products = JsonSerializer.Deserialize<List<ProductViewModel>>(valueProp.GetRawText(), options);
                        }
                    }

                    if (products == null)
                    {
                        products = JsonSerializer.Deserialize<List<ProductViewModel>>(content, options);
                    }
                }

                var result = products ?? new List<ProductViewModel>();
                SetProductImageUrls(result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CatalogService] Error fetching search products");
                return new List<ProductViewModel>();
            }
        }
        public async Task<List<ProductViewModel>> GetFeaturedProductsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("products/featured");

                if (!response.IsSuccessStatusCode)
                    return new List<ProductViewModel>();

                var content = await response.Content.ReadAsStringAsync();
                var products = JsonSerializer.Deserialize<List<ProductViewModel>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                var result = products ?? new List<ProductViewModel>();
                SetProductImageUrls(result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CatalogService] Error fetching featured products");
                return new List<ProductViewModel>();
            }
        }

        public async Task<ProductViewModel?> GetProductByIdAsync(string id)
        {
            var response = await _httpClient.GetAsync($"products/{id}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var product = JsonSerializer.Deserialize<ProductViewModel>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            SetProductImageUrls(product);
            return product;
        }

        public async Task<ProductViewModel?> GetProductBySlugAsync(string slug)
        {
            var response = await _httpClient.GetAsync($"products/slug/{slug}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var product = JsonSerializer.Deserialize<ProductViewModel>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            SetProductImageUrls(product);
            return product;
        }

        public async Task<List<CategoryViewModel>> GetAllCategoriesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("categories");

                if (!response.IsSuccessStatusCode)
                    return new List<CategoryViewModel>();

                var content = await response.Content.ReadAsStringAsync();
                var categories = JsonSerializer.Deserialize<List<CategoryViewModel>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return categories ?? new List<CategoryViewModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CatalogService] Error fetching categories");
                return new List<CategoryViewModel>();
            }
        }

        public async Task<List<CategoryViewModel>> SearchCategoriesAsync(string keyword)
        {
            var rootCategories = await GetAllCategoriesAsync();
            if (string.IsNullOrWhiteSpace(keyword)) return new List<CategoryViewModel>();

            var allCategories = new List<CategoryViewModel>();
            
            void FlattenCategories(IEnumerable<CategoryViewModel> source)
            {
                foreach (var item in source)
                {
                    allCategories.Add(item);
                    if (item.Children != null && item.Children.Any())
                    {
                        FlattenCategories(item.Children);
                    }
                }
            }
            
            FlattenCategories(rootCategories);

            return allCategories
                .Select(c => new { Category = c, Score = GetSearchScore(c.Name, keyword) })
                .Where(x => x.Score < int.MaxValue)
                .OrderBy(x => x.Score)
                .ThenBy(x => x.Category.Name)
                .Select(x => x.Category)
                .ToList();
        }

        public async Task<List<string>> SearchBrandsAsync(string keyword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyword)) return new List<string>();

                var response = await _httpClient.GetAsync($"products/brands?q={Uri.EscapeDataString(keyword)}");
                if (!response.IsSuccessStatusCode) return new List<string>();

                var content = await response.Content.ReadAsStringAsync();
                var brands = System.Text.Json.JsonSerializer.Deserialize<List<string>>(content, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return brands ?? new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CatalogService] Error fetching brands");
                return new List<string>();
            }
        }

        public async Task<List<SearchSuggestionViewModel>> SearchSuggestionsAsync(string keyword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(keyword)) return new List<SearchSuggestionViewModel>();

                var response = await _httpClient.GetAsync($"products/search/suggestions?q={Uri.EscapeDataString(keyword)}");
                if (!response.IsSuccessStatusCode) return new List<SearchSuggestionViewModel>();

                var content = await response.Content.ReadAsStringAsync();
                var suggestions = JsonSerializer.Deserialize<List<SearchSuggestionViewModel>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<SearchSuggestionViewModel>();

                foreach (var suggestion in suggestions.Where(x => string.Equals(x.Type, "product", StringComparison.OrdinalIgnoreCase)))
                {
                    if (string.IsNullOrWhiteSpace(suggestion.ImageUrl))
                    {
                        suggestion.ImageUrl = ProductViewModel.DefaultImageUrl;
                    }
                    else if (!suggestion.ImageUrl.StartsWith("http") && !suggestion.ImageUrl.StartsWith("/"))
                    {
                        var imageBaseUrl = !string.IsNullOrWhiteSpace(_settings.PhotoBaseUrl)
                            ? _settings.PhotoBaseUrl
                            : _settings.PhotoStockUri;

                        suggestion.ImageUrl = $"{imageBaseUrl.TrimEnd('/')}/{suggestion.ImageUrl.TrimStart('/')}";
                    }
                }

                return suggestions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CatalogService] Error fetching search suggestions");
                return new List<SearchSuggestionViewModel>();
            }
        }

        public async Task<SearchIndexStatusViewModel?> GetSearchIndexStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("products/search/status");
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<SearchIndexStatusViewModel>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CatalogService] Error fetching search index status");
                return null;
            }
        }

        public async Task<ReindexResultViewModel?> ReindexSearchIndexAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("products/search/reindex", null);
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<ReindexResultViewModel>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CatalogService] Error rebuilding search index");
                return null;
            }
        }

        public async Task<SearchIndexDocumentPageViewModel> GetSearchIndexDocumentPreviewsAsync(int page = 1, int pageSize = 100)
        {
            try
            {
                var response = await _httpClient.GetAsync($"products/search/documents?page={page}&pageSize={pageSize}");
                if (!response.IsSuccessStatusCode) return new SearchIndexDocumentPageViewModel();

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<SearchIndexDocumentPageViewModel>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new SearchIndexDocumentPageViewModel();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CatalogService] Error fetching search index document previews");
                return new SearchIndexDocumentPageViewModel();
            }
        }

        public async Task<List<ProductViewModel>> GetProductsByCategoryAsync(string categoryId)
        {
            var response = await _httpClient.GetAsync($"products/category/{categoryId}");

            if (!response.IsSuccessStatusCode)
                return new List<ProductViewModel>();

            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            List<ProductViewModel>? products = null;

            if (!string.IsNullOrWhiteSpace(content))
            {
                if (content.TrimStart().StartsWith("{"))
                {
                    using var doc = JsonDocument.Parse(content);
                    if (doc.RootElement.TryGetProperty("value", out var valueProp))
                    {
                        products = JsonSerializer.Deserialize<List<ProductViewModel>>(valueProp.GetRawText(), options);
                    }
                }

                if (products == null)
                {
                    products = JsonSerializer.Deserialize<List<ProductViewModel>>(content, options);
                }
            }

            var result = products ?? new List<ProductViewModel>();
            SetProductImageUrls(result);
            return result;
        }
        public async Task<CategoryViewModel?> CreateCategoryAsync(CategoryCreateInput model)
        {
            var response = await _httpClient.PostAsJsonAsync("categories", model);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var category = JsonSerializer.Deserialize<CategoryViewModel>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return category;
        }

        public async Task<CategoryViewModel?> UpdateCategoryAsync(string id, CategoryCreateInput model)
        {
            var response = await _httpClient.PutAsJsonAsync($"categories/{id}", model);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var category = JsonSerializer.Deserialize<CategoryViewModel>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return category;
        }

        public async Task<bool> AddAttributeAsync(string categoryId, CategoryAttributeInput model)
        {
            var response = await _httpClient.PostAsJsonAsync($"categories/{categoryId}/attributes", model);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateAttributeAsync(string categoryId, string attributeId, CategoryAttributeInput model)
        {
            var response = await _httpClient.PutAsJsonAsync($"categories/{categoryId}/attributes/{attributeId}", model);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteAttributeAsync(string categoryId, string attributeId)
        {
            var response = await _httpClient.DeleteAsync($"categories/{categoryId}/attributes/{attributeId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<CategoryViewModel?> GetCategoryByIdAsync(string id)
        {
            var response = await _httpClient.GetAsync($"categories/{id}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var category = JsonSerializer.Deserialize<CategoryViewModel>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return category;
        }

        public async Task<CategoryViewModel?> GetCategoryBySlugAsync(string slug)
        {
            var response = await _httpClient.GetAsync($"categories/slug/{slug}");

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var category = JsonSerializer.Deserialize<CategoryViewModel>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return category;
        }

        public async Task<ProductViewModel?> CreateProductAsync(ProductCreateInput model)
        {
            var response = await _httpClient.PostAsJsonAsync("products", model);

            if (!response.IsSuccessStatusCode)
                return null;

            var content = await response.Content.ReadAsStringAsync();
            var product = JsonSerializer.Deserialize<ProductViewModel>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return product;
        }

        public async Task<bool> UpdateProductAsync(ProductUpdateInput model)
        {
            var response = await _httpClient.PutAsJsonAsync("products", model);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteProductAsync(string id)
        {
            var response = await _httpClient.DeleteAsync($"products/{id}");
            return response.IsSuccessStatusCode;
        }
    }
}
