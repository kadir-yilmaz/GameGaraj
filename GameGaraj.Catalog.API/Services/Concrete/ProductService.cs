using AutoMapper;
using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Repositories.Abstract;
using GameGaraj.Catalog.API.Services.Abstract;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using GameGaraj.Shared.Helpers;

namespace GameGaraj.Catalog.API.Services.Concrete
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ILogger<ProductService> _logger;
        private readonly IMapper _mapper;
        private readonly ElasticsearchClient _elasticClient;

        public ProductService(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IMapper mapper,
            ILogger<ProductService> logger,
            ElasticsearchClient elasticClient)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _logger = logger;
            _elasticClient = elasticClient;
        }

        public async Task<List<ProductDto>> GetAllAsync(
            string? categoryId = null,
            string? sortBy = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            Dictionary<string, string>? specs = null)
        {
            _logger.LogInformation($"========== ProductService.GetAllAsync START ==========");
            _logger.LogInformation($"[ProductService] Parameters - CategoryId: {categoryId}, Sort: {sortBy}, MinPrice: {minPrice}, MaxPrice: {maxPrice}");

            // 1. Get products (with category filter if provided)
            List<Product> products;

            if (!string.IsNullOrEmpty(categoryId))
            {
                _logger.LogInformation($"[ProductService] Category filter active - CategoryId: {categoryId}");

                // Get all descendant categories
                var allCategoryIds = await GetCategoryDescendants(categoryId);
                allCategoryIds.Add(categoryId); // Include parent

                _logger.LogInformation($"[ProductService] Found {allCategoryIds.Count} categories (including parent)");
                _logger.LogInformation($"[ProductService] Category IDs: {string.Join(", ", allCategoryIds)}");

                products = await _productRepository.GetByCategoryIdsAsync(allCategoryIds);
                _logger.LogInformation($"[ProductService] Repository returned {products.Count} products");
            }
            else
            {
                _logger.LogInformation($"[ProductService] No category filter - Getting all products");
                products = await _productRepository.GetAllAsync();
                _logger.LogInformation($"[ProductService] Repository returned {products.Count} products");
            }

            // 2. Apply filters in memory (for complex filters)
            var filtered = products.AsEnumerable();
            var initialCount = products.Count;

            if (minPrice.HasValue && minPrice.Value > 0)
            {
                filtered = filtered.Where(p => p.Price >= minPrice.Value);
                _logger.LogInformation($"[ProductService] After minPrice filter: {filtered.Count()} products");
            }

            if (maxPrice.HasValue && maxPrice.Value > 0)
            {
                filtered = filtered.Where(p => p.Price <= maxPrice.Value);
                _logger.LogInformation($"[ProductService] After maxPrice filter: {filtered.Count()} products");
            }

            if (specs != null && specs.Any())
            {
                // CRITICAL FIX: Remove reserved query parameters from specs
                // These are handled separately and should not be treated as product specs
                var reservedParams = new[] { "categoryId", "sortBy", "minPrice", "maxPrice", "search" };
                var actualSpecs = specs.Where(s => !reservedParams.Contains(s.Key, StringComparer.OrdinalIgnoreCase))
                                      .ToDictionary(s => s.Key, s => s.Value);

                if (actualSpecs.Any())
                {
                    _logger.LogInformation($"[ProductService] Applying {actualSpecs.Count} spec filters (filtered out {specs.Count - actualSpecs.Count} reserved params)");
                    foreach (var spec in actualSpecs)
                    {
                        if (!string.IsNullOrEmpty(spec.Value))
                        {
                            var key = spec.Key;
                            var val = spec.Value;

                            // Support comma-separated values for multi-select (e.g., "8GB,16GB")
                            var allowedValues = val.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                  .Select(v => v.Trim())
                                                  .ToList();

                            if (allowedValues.Count > 1)
                            {
                                // Multi-select: Product must match ANY of the values (OR logic)
                                filtered = filtered.Where(p =>
                                    p.Specs.ContainsKey(key) && allowedValues.Contains(p.Specs[key]));
                                _logger.LogInformation($"[ProductService] After multi-select spec filter {key}=[{string.Join(", ", allowedValues)}]: {filtered.Count()} products");
                            }
                            else
                            {
                                // Single value: Exact match
                                filtered = filtered.Where(p =>
                                    p.Specs.ContainsKey(key) && p.Specs[key] == val);
                                _logger.LogInformation($"[ProductService] After spec filter {key}={val}: {filtered.Count()} products");
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogInformation($"[ProductService] No actual spec filters to apply (all {specs.Count} params were reserved)");
                }
            }

            // 3. Sorting
            filtered = sortBy?.ToLower() switch
            {
                "price_asc" => filtered.OrderBy(p => p.Price),
                "price_desc" => filtered.OrderByDescending(p => p.Price),
                "newest" => filtered.OrderByDescending(p => p.CreatedAt),
                _ => filtered.OrderByDescending(p => p.CreatedAt)
            };

            var result = filtered.ToList();
            _logger.LogInformation($"[ProductService] Final result: {result.Count} products (started with {initialCount})");

            // 4. Map to DTOs
            var productDtos = _mapper.Map<List<ProductDto>>(result);

            // 5. Enrich with category names
            if (result.Any())
            {
                var distinctCatIds = result.Select(p => p.CategoryId).Distinct().ToList();
                var categories = await _categoryRepository.GetAllAsync();
                var catNames = categories.ToDictionary(c => c.Id, c => c.Name);

                foreach (var dto in productDtos)
                {
                    if (!string.IsNullOrEmpty(dto.CategoryId) &&
                        catNames.TryGetValue(dto.CategoryId, out var name))
                    {
                        dto.CategoryName = name;
                    }
                }
            }

            _logger.LogInformation($"========== ProductService.GetAllAsync END - Returning {productDtos.Count} products ==========");
            return productDtos;
        }

        private async Task<List<string>> GetCategoryDescendants(string parentId)
        {
            var children = await _categoryRepository.GetByParentIdAsync(parentId);
            var descendants = new List<string>(children.Select(c => c.Id));

            foreach (var child in children)
            {
                descendants.AddRange(await GetCategoryDescendants(child.Id));
            }

            return descendants;
        }

        public async Task<List<ProductDto>> GetFeaturedProductsAsync()
        {
            var products = await _productRepository.GetFeaturedAsync(10);
            return _mapper.Map<List<ProductDto>>(products);
        }

        public async Task<ProductDto?> GetByIdAsync(string id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return null;

            var dto = _mapper.Map<ProductDto>(product);

            // Get category name
            var category = await _categoryRepository.GetByIdAsync(product.CategoryId);
            dto.CategoryName = category?.Name;

            return dto;
        }

        public async Task<ProductDto?> GetBySlugAsync(string slug)
        {
            var product = await _productRepository.GetBySlugAsync(slug);
            if (product == null)
                return null;

            var dto = _mapper.Map<ProductDto>(product);

            // Get category name
            var category = await _categoryRepository.GetByIdAsync(product.CategoryId);
            dto.CategoryName = category?.Name;

            return dto;
        }

        public async Task<List<ProductDto>> GetByCategoryIdAsync(string categoryId)
        {
            return await GetAllAsync(categoryId);
        }

        public async Task<ProductDto> CreateAsync(ProductCreateDto dto)
        {
            var product = new Product
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                Brand = dto.Brand,
                Slug = UrlHelper.GenerateSlug(dto.Brand, dto.Name),
                Description = dto.Description,
                Price = dto.Price,
                Stock = dto.Stock,
                ReservedStock = 0,
                IsActive = dto.IsActive,
                IsFeatured = dto.IsFeatured,
                ImageUrls = dto.ImageUrls.Take(5).ToList(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CategoryId = dto.CategoryId,
                Specs = dto.Specs
            };

            await _productRepository.CreateAsync(product);

            try 
            {
                var request = new IndexRequest<Product>(product, "products", product.Id);
                await _elasticClient.IndexAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing product {Id} to Elasticsearch", product.Id);
            }

            return _mapper.Map<ProductDto>(product);
        }

        public async Task<bool> UpdateAsync(ProductUpdateDto dto)
        {
            var product = await _productRepository.GetByIdAsync(dto.Id);
            if (product == null)
                return false;

            product.Name = dto.Name;
            product.Brand = dto.Brand;
            product.Slug = UrlHelper.GenerateSlug(dto.Brand, dto.Name);
            product.Description = dto.Description;
            product.Price = dto.Price;
            product.Stock = dto.Stock;
            product.IsActive = dto.IsActive;
            product.IsFeatured = dto.IsFeatured;
            product.ImageUrls = dto.ImageUrls.Take(5).ToList();
            product.CategoryId = dto.CategoryId;
            product.Specs = dto.Specs;
            product.UpdatedAt = DateTime.UtcNow;

            var result = await _productRepository.UpdateAsync(product);
            if (result)
            {
                try 
                {
                    await _elasticClient.UpdateAsync<Product, Product>("products", product.Id, u => u.Doc(product));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating product {Id} in Elasticsearch", product.Id);
                }
            }
            return result;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _productRepository.DeleteAsync(id);
            if (result)
            {
                try 
                {
                    await _elasticClient.DeleteAsync("products", id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting product {Id} from Elasticsearch", id);
                }
            }
            return result;
        }

        public async Task<List<ProductDto>> SearchAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<ProductDto>();

            var response = await _elasticClient.SearchAsync<Product>(s => s
                .Index("products")
                .Query(q => q
                    .MultiMatch(m => m
                        .Fields(new[] { "name^3", "brand^2" })
                        .Query(keyword)
                        .Fuzziness(new Fuzziness("AUTO"))
                    )
                )
                .Size(20)
            );

            if (!response.IsValidResponse)
            {
                _logger.LogError("Elasticsearch query failed: {Error}", response.DebugInformation);
                return new List<ProductDto>();
            }

            var products = response.Documents.ToList();
            var dtos = _mapper.Map<List<ProductDto>>(products);
            return dtos;
        }

        public async Task<List<string>> GetBrandsByKeywordAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<string>();

            var response = await _elasticClient.SearchAsync<Product>(s => s
                .Index("products")
                .Query(q => q
                    .Wildcard(w => w
                        .Field(f => f.Brand)
                        .Value($"*{keyword.ToLower()}*")
                        .CaseInsensitive(true)
                    )
                )
                .Size(0)
                .Aggregations(a => a
                    .Add("brands", agg => agg
                        .Terms(t => t
                            .Field("brand.keyword")
                            .Size(10)
                        )
                    )
                )
            );

            if (!response.IsValidResponse) return new List<string>();

            var bucket = response.Aggregations?.GetStringTerms("brands");
            return bucket?.Buckets.Select(b => b.Key.ToString()).ToList() ?? new List<string>();
        }
    }
}
