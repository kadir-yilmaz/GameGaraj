using AutoMapper;
using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Services.Abstract;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace GameGaraj.Catalog.API.Services.Concrete
{
    public class ProductQueryService : IProductQueryService
    {
        private readonly CatalogDbContext _context;
        private readonly ILogger<ProductQueryService> _logger;
        private readonly IMapper _mapper;
        private readonly ElasticsearchClient _elasticClient;

        public ProductQueryService(
            CatalogDbContext context,
            IMapper mapper,
            ILogger<ProductQueryService> logger,
            ElasticsearchClient elasticClient)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
            _elasticClient = elasticClient;
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

        public async Task<List<ProductDto>> GetAllAsync(
            string? categoryId = null,
            string? sortBy = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            Dictionary<string, string>? specs = null,
            string? brand = null)
        {
            _logger.LogInformation($"========== ProductQueryService.GetAllAsync START ==========");
            _logger.LogInformation($"[ProductQueryService] Parameters - CategoryId: {categoryId}, Sort: {sortBy}, MinPrice: {minPrice}, MaxPrice: {maxPrice}, Brand: {brand}");

            var elasticResult = await GetAllFromElasticAsync(categoryId, sortBy, minPrice, maxPrice, specs, brand);
            if (elasticResult != null)
            {
                _logger.LogInformation($"========== ProductQueryService.GetAllAsync END - Returning {elasticResult.Count} products from Elasticsearch ==========");
                return elasticResult;
            }

            _logger.LogWarning("[ProductQueryService] Elasticsearch listing unavailable or empty. Falling back to PostgreSQL.");
            return await GetAllFromPostgresAsync(categoryId, sortBy, minPrice, maxPrice, specs, brand);
        }

        public async Task<PagedResultDto<ProductDto>> GetAdminPageAsync(
            string? query = null,
            string? categoryId = null,
            bool? isFeatured = null,
            bool? isActive = null,
            string? stockState = null,
            int page = 1,
            int pageSize = 20)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 10, 100);

            var productsQuery = _context.Products.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var normalizedQuery = query.Trim().ToLower();
                var pattern = $"%{normalizedQuery}%";
                productsQuery = productsQuery.Where(product =>
                    EF.Functions.Like((product.Id ?? string.Empty).ToLower(), pattern) ||
                    EF.Functions.Like((product.Name ?? string.Empty).ToLower(), pattern) ||
                    EF.Functions.Like((product.Brand ?? string.Empty).ToLower(), pattern) ||
                    EF.Functions.Like((product.Slug ?? string.Empty).ToLower(), pattern));
            }

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                productsQuery = productsQuery.Where(product => product.CategoryId == categoryId);
            }

            if (isFeatured.HasValue)
            {
                productsQuery = productsQuery.Where(product => product.IsFeatured == isFeatured.Value);
            }

            if (isActive.HasValue)
            {
                productsQuery = productsQuery.Where(product => product.IsActive == isActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(stockState))
            {
                productsQuery = stockState.ToLowerInvariant() switch
                {
                    "in" => productsQuery.Where(product => product.Stock > 0),
                    "out" => productsQuery.Where(product => product.Stock <= 0),
                    _ => productsQuery
                };
            }

            var totalCount = await productsQuery.LongCountAsync();
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            var products = await productsQuery
                .OrderByDescending(product => product.UpdatedAt ?? product.CreatedAt)
                .ThenBy(product => product.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var productDtos = _mapper.Map<List<ProductDto>>(products);

            if (products.Any())
            {
                var categoryIds = products
                    .Select(product => product.CategoryId)
                    .Where(category => !string.IsNullOrWhiteSpace(category))
                    .Distinct()
                    .ToList();

                var categoryNames = await _context.Categories
                    .AsNoTracking()
                    .Where(category => categoryIds.Contains(category.Id))
                    .ToDictionaryAsync(category => category.Id, category => category.Name);

                foreach (var dto in productDtos)
                {
                    if (!string.IsNullOrWhiteSpace(dto.CategoryId) &&
                        categoryNames.TryGetValue(dto.CategoryId, out var categoryName))
                    {
                        dto.CategoryName = categoryName;
                    }
                }
            }

            return new PagedResultDto<ProductDto>
            {
                Items = productDtos,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };
        }

        private async Task<List<ProductDto>?> GetAllFromElasticAsync(
            string? categoryId = null,
            string? sortBy = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            Dictionary<string, string>? specs = null,
            string? brand = null)
        {
            var response = await _elasticClient.SearchAsync<ProductSearchDocument>(s => s
                .Index("products")
                .Query(q => q.Bool(b => b.Filter(f => f.Term(t => t.Field("isActive").Value(true)))))
                .Size(1000)
            );

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch product listing query failed: {Error}", response.DebugInformation);
                return null;
            }

            var documents = response.Documents.ToList();
            if (!documents.Any())
            {
                return null;
            }

            var filtered = documents.AsEnumerable();
            var categoryIds = new List<string>();

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                categoryIds = await GetCategoryDescendants(categoryId);
                categoryIds.Add(categoryId);
                filtered = filtered.Where(p => categoryIds.Contains(p.CategoryId));
            }

            if (minPrice.HasValue && minPrice.Value > 0)
            {
                filtered = filtered.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue && maxPrice.Value > 0)
            {
                filtered = filtered.Where(p => p.Price <= maxPrice.Value);
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                var normalizedBrand = brand.Trim();
                filtered = filtered.Where(p =>
                    string.Equals(p.Brand?.Trim(), normalizedBrand, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(p.Name) &&
                        p.Name.Trim().StartsWith($"{normalizedBrand} ", StringComparison.OrdinalIgnoreCase)));
            }

            filtered = ApplySpecFilters(filtered, specs);

            filtered = sortBy?.ToLower() switch
            {
                "price_asc" => filtered.OrderBy(p => p.Price),
                "price_desc" => filtered.OrderByDescending(p => p.Price),
                "newest" => filtered.OrderByDescending(p => p.CreatedAt),
                _ => filtered.OrderByDescending(p => p.CreatedAt)
            };

            return filtered.Select(MapSearchDocumentToDto).ToList();
        }

        private async Task<List<ProductDto>> GetAllFromPostgresAsync(
            string? categoryId = null,
            string? sortBy = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            Dictionary<string, string>? specs = null,
            string? brand = null)
        {
            // 1. Get products (with category filter if provided)
            List<Product> products;

            if (!string.IsNullOrEmpty(categoryId))
            {
                _logger.LogInformation($"[ProductQueryService] Category filter active - CategoryId: {categoryId}");

                // Get all descendant categories
                var allCategoryIds = await GetCategoryDescendants(categoryId);
                allCategoryIds.Add(categoryId); // Include parent

                _logger.LogInformation($"[ProductQueryService] Found {allCategoryIds.Count} categories (including parent)");
                _logger.LogInformation($"[ProductQueryService] Category IDs: {string.Join(", ", allCategoryIds)}");

                products = await _context.Products
                    .AsNoTracking()
                    .Where(p => allCategoryIds.Contains(p.CategoryId))
                    .ToListAsync();
                _logger.LogInformation($"[ProductQueryService] Query returned {products.Count} products");
            }
            else
            {
                _logger.LogInformation($"[ProductQueryService] No category filter - Getting all products");
                products = await _context.Products.AsNoTracking().ToListAsync();
                _logger.LogInformation($"[ProductQueryService] Query returned {products.Count} products");
            }

            // 2. Apply filters in memory (for complex filters)
            var filtered = products.AsEnumerable();
            var initialCount = products.Count;

            if (minPrice.HasValue && minPrice.Value > 0)
            {
                filtered = filtered.Where(p => p.Price >= minPrice.Value);
                _logger.LogInformation($"[ProductQueryService] After minPrice filter: {filtered.Count()} products");
            }

            if (maxPrice.HasValue && maxPrice.Value > 0)
            {
                filtered = filtered.Where(p => p.Price <= maxPrice.Value);
                _logger.LogInformation($"[ProductQueryService] After maxPrice filter: {filtered.Count()} products");
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                var normalizedBrand = brand.Trim();
                filtered = filtered.Where(p =>
                    string.Equals(p.Brand?.Trim(), normalizedBrand, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(p.Name) &&
                        p.Name.Trim().StartsWith($"{normalizedBrand} ", StringComparison.OrdinalIgnoreCase)));
                _logger.LogInformation($"[ProductQueryService] After brand filter {normalizedBrand}: {filtered.Count()} products");
            }

            if (specs != null && specs.Any())
            {
                // CRITICAL FIX: Remove reserved query parameters from specs
                // These are handled separately and should not be treated as product specs
                var reservedParams = new[] { "category", "categoryId", "sortBy", "minPrice", "maxPrice", "search", "brand" };
                var actualSpecs = specs.Where(s => !reservedParams.Contains(s.Key, StringComparer.OrdinalIgnoreCase))
                                      .ToDictionary(s => s.Key, s => s.Value);

                if (actualSpecs.Any())
                {
                    _logger.LogInformation($"[ProductQueryService] Applying {actualSpecs.Count} spec filters (filtered out {specs.Count - actualSpecs.Count} reserved params)");
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
                                _logger.LogInformation($"[ProductQueryService] After multi-select spec filter {key}=[{string.Join(", ", allowedValues)}]: {filtered.Count()} products");
                            }
                            else
                            {
                                // Single value: Exact match
                                filtered = filtered.Where(p =>
                                    p.Specs.ContainsKey(key) && p.Specs[key] == val);
                                _logger.LogInformation($"[ProductQueryService] After spec filter {key}={val}: {filtered.Count()} products");
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogInformation($"[ProductQueryService] No actual spec filters to apply (all {specs.Count} params were reserved)");
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
            _logger.LogInformation($"[ProductQueryService] Final result: {result.Count} products (started with {initialCount})");

            // 4. Map to DTOs
            var productDtos = _mapper.Map<List<ProductDto>>(result);

            // 5. Enrich with category names
            if (result.Any())
            {
                var distinctCatIds = result.Select(p => p.CategoryId).Distinct().ToList();
                var categories = await _context.Categories.AsNoTracking().ToListAsync();
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

            _logger.LogInformation($"========== ProductQueryService.GetAllAsync END - Returning {productDtos.Count} products ==========");
            return productDtos;
        }

        private static IEnumerable<ProductSearchDocument> ApplySpecFilters(IEnumerable<ProductSearchDocument> documents, Dictionary<string, string>? specs)
        {
            var actualSpecs = GetActualSpecs(specs);
            foreach (var spec in actualSpecs)
            {
                if (string.IsNullOrWhiteSpace(spec.Value))
                    continue;

                var key = spec.Key;
                var allowedValues = spec.Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(v => v.Trim())
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .ToList();

                if (!allowedValues.Any())
                    continue;

                documents = documents.Where(p =>
                    p.Specs.ContainsKey(key) &&
                    allowedValues.Contains(p.Specs[key], StringComparer.OrdinalIgnoreCase));
            }

            return documents;
        }

        private static Dictionary<string, string> GetActualSpecs(Dictionary<string, string>? specs)
        {
            if (specs == null || !specs.Any())
                return new Dictionary<string, string>();

            var reservedParams = new[] { "category", "categoryId", "sortBy", "minPrice", "maxPrice", "search", "brand" };
            return specs
                .Where(s => !reservedParams.Contains(s.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(s => s.Key, s => s.Value);
        }

        private async Task<List<string>> GetCategoryDescendants(string parentId)
        {
            var children = await _context.Categories
                .AsNoTracking()
                .Where(c => c.ParentId == parentId)
                .ToListAsync();
            var descendants = new List<string>(children.Select(c => c.Id));

            foreach (var child in children)
            {
                descendants.AddRange(await GetCategoryDescendants(child.Id));
            }

            return descendants;
        }

        public async Task<List<ProductDto>> GetFeaturedProductsAsync()
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.IsFeatured && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .ToListAsync();
            return _mapper.Map<List<ProductDto>>(products);
        }

        public async Task<ProductDto?> GetByIdAsync(string id)
        {
            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
                return null;

            var dto = _mapper.Map<ProductDto>(product);

            // Get category name
            var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == product.CategoryId);
            dto.CategoryName = category?.Name;

            return dto;
        }

        public async Task<ProductDto?> GetBySlugAsync(string slug)
        {
            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug);
            if (product == null)
                return null;

            var dto = _mapper.Map<ProductDto>(product);

            // Get category name
            var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == product.CategoryId);
            dto.CategoryName = category?.Name;

            return dto;
        }

        public async Task<List<ProductDto>> GetByCategoryIdAsync(string categoryId)
        {
            return await GetAllAsync(categoryId);
        }

        public async Task<List<ProductDto>> SearchAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<ProductDto>();

            var response = await _elasticClient.SearchAsync<ProductSearchDocument>(s => s
                .Index("products")
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                            .MultiMatch(mm => mm
                                .Fields(new[]
                                {
                                    "name^5",
                                    "brand^4",
                                    "categoryName^3",
                                    "specValues^2",
                                    "searchText",
                                    "description"
                                })
                                .Query(keyword)
                                .Fuzziness(new Fuzziness("AUTO"))
                                .MinimumShouldMatch("70%")
                                .PrefixLength(1)
                            )
                        )
                        .Filter(f => f.Term(t => t.Field("isActive").Value(true)))
                        .Should(
                            sh => sh.Term(t => t.Field("isFeatured").Value(true).Boost(2)),
                            sh => sh.Term(t => t.Field("inStock").Value(true).Boost(1.5f))
                        )
                    )
                )
                .Size(20)
            );

            if (!response.IsValidResponse)
            {
                _logger.LogError("Elasticsearch query failed: {Error}", response.DebugInformation);
                return new List<ProductDto>();
            }

            return response.Documents.Select(MapSearchDocumentToDto).ToList();
        }

        private static ProductDto MapSearchDocumentToDto(ProductSearchDocument document)
        {
            return new ProductDto
            {
                Id = document.Id,
                Name = document.Name,
                Brand = document.Brand,
                Slug = document.Slug,
                Description = document.Description,
                Price = document.Price,
                Stock = document.Stock,
                ReservedStock = document.ReservedStock,
                AvailableStock = document.AvailableStock,
                IsActive = document.IsActive,
                IsFeatured = document.IsFeatured,
                ImageUrls = document.ImageUrls,
                CreatedAt = document.CreatedAt,
                CategoryId = document.CategoryId,
                CategoryName = document.CategoryName,
                Specs = document.Specs
            };
        }

        public async Task<List<SearchSuggestionDto>> GetSuggestionsAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<SearchSuggestionDto>();

            var response = await _elasticClient.SearchAsync<ProductSearchDocument>(s => s
                .Index("products")
                .Query(q => q
                    .Bool(b => b
                        .Must(m => m
                            .MultiMatch(mm => mm
                                .Fields(new[]
                                {
                                    "name^5",
                                    "brand^4",
                                    "categoryName^3",
                                    "specValues^2",
                                    "searchText"
                                })
                                .Query(keyword)
                                .Fuzziness(new Fuzziness("AUTO"))
                                .MinimumShouldMatch("60%")
                                .PrefixLength(1)
                            )
                        )
                        .Filter(f => f.Term(t => t.Field("isActive").Value(true)))
                    )
                )
                .Size(20)
            );

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch suggestion query failed: {Error}", response.DebugInformation);
                return await GetSuggestionsFromPostgresAsync(keyword);
            }

            var suggestions = new List<SearchSuggestionDto>();
            var documents = response.Documents.ToList();
            if (!documents.Any())
            {
                return await GetSuggestionsFromPostgresAsync(keyword);
            }

            suggestions.AddRange(documents
                .Take(10)
                .Select(p => new SearchSuggestionDto
                {
                    Type = "product",
                    Id = p.Id,
                    Name = p.Name,
                    Slug = p.Slug,
                    ImageUrl = p.ImageUrls.FirstOrDefault(),
                    Price = p.Price
                }));

            suggestions.AddRange(documents
                .Where(p => !string.IsNullOrWhiteSpace(p.Brand))
                .GroupBy(p => p.Brand.Trim(), StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .Select(g => new SearchSuggestionDto
                {
                    Type = "brand",
                    Id = g.Key,
                    Name = g.Key
                }));

            suggestions.AddRange(documents
                .Where(p => !string.IsNullOrWhiteSpace(p.CategoryName))
                .GroupBy(p => new { p.CategoryId, p.CategoryName })
                .Take(5)
                .Select(g => new SearchSuggestionDto
                {
                    Type = "category",
                    Id = g.Key.CategoryId,
                    Name = g.Key.CategoryName,
                    Slug = g.FirstOrDefault()?.CategorySlug
                }));

            return suggestions;
        }

        private async Task<List<SearchSuggestionDto>> GetSuggestionsFromPostgresAsync(string keyword)
        {
            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive)
                .ToListAsync();
            var categories = await _context.Categories.AsNoTracking().ToListAsync();
            var categoryLookup = categories.ToDictionary(c => c.Id);

            var scoredProducts = products
                .Select(p =>
                {
                    categoryLookup.TryGetValue(p.CategoryId, out var category);
                    var searchable = string.Join(' ', new[]
                    {
                        p.Name,
                        p.Brand,
                        category?.Name,
                        p.Description,
                        string.Join(' ', p.Specs.Keys),
                        string.Join(' ', p.Specs.Values)
                    }.Where(x => !string.IsNullOrWhiteSpace(x)));

                    return new
                    {
                        Product = p,
                        Category = category,
                        Score = Math.Min(GetSearchScore(searchable, keyword), GetSearchScore(p.Brand, keyword))
                    };
                })
                .Where(x => x.Score < int.MaxValue)
                .OrderBy(x => x.Score)
                .ThenByDescending(x => x.Product.IsFeatured)
                .ThenByDescending(x => x.Product.AvailableStock)
                .ThenByDescending(x => x.Product.CreatedAt)
                .ToList();

            var suggestions = new List<SearchSuggestionDto>();

            suggestions.AddRange(scoredProducts
                .Take(10)
                .Select(x => new SearchSuggestionDto
                {
                    Type = "product",
                    Id = x.Product.Id,
                    Name = x.Product.Name,
                    Slug = x.Product.Slug,
                    ImageUrl = x.Product.ImageUrls.FirstOrDefault(),
                    Price = x.Product.Price
                }));

            suggestions.AddRange(scoredProducts
                .Where(x => !string.IsNullOrWhiteSpace(x.Product.Brand))
                .GroupBy(x => x.Product.Brand.Trim(), StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .Select(g => new SearchSuggestionDto
                {
                    Type = "brand",
                    Id = g.Key,
                    Name = g.Key
                }));

            suggestions.AddRange(scoredProducts
                .Where(x => x.Category != null)
                .GroupBy(x => x.Category!.Id)
                .Take(5)
                .Select(g =>
                {
                    var category = g.First().Category!;
                    return new SearchSuggestionDto
                    {
                        Type = "category",
                        Id = category.Id,
                        Name = category.Name,
                        Slug = category.Slug
                    };
                }));

            return suggestions;
        }

        public async Task<List<string>> GetBrandsByKeywordAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<string>();

            var response = await _elasticClient.SearchAsync<ProductSearchDocument>(s => s
                .Index("products")
                .Query(q => q
                    .Wildcard(w => w
                        .Field("brand.keyword")
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

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch brand suggestion failed, falling back to database: {Error}", response.DebugInformation);
                return await GetBrandSuggestionsFromDatabaseAsync(keyword);
            }

            var bucket = response.Aggregations?.GetStringTerms("brands");
            var brands = bucket?.Buckets.Select(b => b.Key.ToString()).Where(b => !string.IsNullOrWhiteSpace(b)).ToList() ?? new List<string>();

            return brands.Any()
                ? brands
                : await GetBrandSuggestionsFromDatabaseAsync(keyword);
        }

        private async Task<List<string>> GetBrandSuggestionsFromDatabaseAsync(string keyword)
        {
            var products = await _context.Products.AsNoTracking().ToListAsync();

            return products
                .Select(p => p.Brand?.Trim())
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(b => new { Brand = b!, Score = GetSearchScore(b!, keyword) })
                .Where(x => x.Score < int.MaxValue)
                .OrderBy(x => x.Score)
                .ThenBy(x => x.Brand)
                .Take(10)
                .Select(x => x.Brand)
                .ToList();
        }

        public async Task<SearchFacetResultDto> GetSearchFacetsAsync(string? keyword)
        {
            var response = string.IsNullOrWhiteSpace(keyword)
                ? await _elasticClient.SearchAsync<ProductSearchDocument>(s => s
                    .Index("products")
                    .Size(0)
                    .Query(q => q.Bool(b => b.Filter(f => f.Term(t => t.Field("isActive").Value(true)))))
                    .Aggregations(a => a
                        .Add("brands", agg => agg
                            .Terms(t => t
                                .Field("brand.keyword")
                                .Size(20)
                            )
                        )
                        .Add("categories", agg => agg
                            .Terms(t => t
                                .Field("categoryName.keyword")
                                .Size(20)
                            )
                        )
                    )
                )
                : await _elasticClient.SearchAsync<ProductSearchDocument>(s => s
                    .Index("products")
                    .Size(0)
                    .Query(q => q.Bool(b => b
                        .Must(m => m
                            .MultiMatch(mm => mm
                                .Fields(new[]
                                {
                                    "name^5",
                                    "brand^4",
                                    "categoryName^3",
                                    "specValues^2",
                                    "searchText"
                                })
                                .Query(keyword)
                                .Fuzziness(new Fuzziness("AUTO"))
                                .MinimumShouldMatch("60%")
                                .PrefixLength(1)
                            )
                        )
                        .Filter(f => f.Term(t => t.Field("isActive").Value(true)))
                    ))
                    .Aggregations(a => a
                        .Add("brands", agg => agg
                            .Terms(t => t
                                .Field("brand.keyword")
                                .Size(20)
                            )
                        )
                        .Add("categories", agg => agg
                            .Terms(t => t
                                .Field("categoryName.keyword")
                                .Size(20)
                            )
                        )
                    )
                );

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch facet query failed: {Error}", response.DebugInformation);
                return new SearchFacetResultDto();
            }

            return new SearchFacetResultDto
            {
                Brands = ReadFacetItems(response.Aggregations?.GetStringTerms("brands")),
                Categories = ReadFacetItems(response.Aggregations?.GetStringTerms("categories"))
            };
        }

        private static List<SearchFacetItemDto> ReadFacetItems(Elastic.Clients.Elasticsearch.Aggregations.StringTermsAggregate? aggregate)
        {
            return aggregate?.Buckets
                .Select(b => new SearchFacetItemDto
                {
                    Value = b.Key.ToString(),
                    Count = b.DocCount
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .ToList() ?? new List<SearchFacetItemDto>();
        }
    }
}
