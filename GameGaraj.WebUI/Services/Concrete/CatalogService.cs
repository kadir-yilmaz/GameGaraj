using GameGaraj.WebUI.Models.Baskets;
using GameGaraj.WebUI.Models.Products;
using GameGaraj.WebUI.Services.Abstract;
using GameGaraj.WebUI.Settings;
using Microsoft.Extensions.Options;
using System.Text;
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
                    var baseUrl = _settings.PhotoStockUri.EndsWith("/") ? _settings.PhotoStockUri : _settings.PhotoStockUri + "/";
                    var path = product.ImageUrls[i].StartsWith("/") ? product.ImageUrls[i].Substring(1) : product.ImageUrls[i];
                    product.ImageUrls[i] = baseUrl + path;
                }
            }
        }

        public async Task<List<ProductViewModel>> GetAllProductsAsync(string? categoryId = null, string? sortBy = null, decimal? minPrice = null, decimal? maxPrice = null, Dictionary<string, string[]>? specs = null) // Kept original specs type
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

                if (specs != null && specs.Any())
                {
                    foreach (var spec in specs)
                    {
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
                .Where(c => c.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
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
