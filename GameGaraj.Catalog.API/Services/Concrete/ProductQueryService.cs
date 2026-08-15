using AutoMapper;
using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Services.Abstract;
using GameGaraj.Shared.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace GameGaraj.Catalog.API.Services.Concrete
{
    public class ProductQueryService : IProductQueryService
    {
        private readonly CatalogDbContext _context;
        private readonly ILogger<ProductQueryService> _logger;
        private readonly IMapper _mapper;
        private readonly IDistributedCache? _cache;

        public ProductQueryService(
            CatalogDbContext context,
            IMapper mapper,
            ILogger<ProductQueryService> logger,
            IDistributedCache? cache = null)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
            _cache = cache;
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

        public async Task<List<ProductDto>> GetAllAsync(
            string? categoryId = null,
            string? sortBy = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            Dictionary<string, string>? specs = null,
            string? brand = null)
        {
            var query = _context.Products.AsNoTracking().Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                var categoryIds = await GetCategoryDescendants(categoryId);
                categoryIds.Add(categoryId);
                query = query.Where(p => categoryIds.Contains(p.CategoryId));
            }

            if (!string.IsNullOrWhiteSpace(brand))
            {
                query = query.Where(p => p.Brand == brand);
            }

            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            query = sortBy?.ToLowerInvariant() switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "newest" => query.OrderByDescending(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            var products = await query.ToListAsync();
            var dtos = _mapper.Map<List<ProductDto>>(products);

            if (specs != null && specs.Any())
            {
                var actualSpecs = GetActualSpecs(specs);
                foreach (var spec in actualSpecs)
                {
                    if (string.IsNullOrWhiteSpace(spec.Value)) continue;
                    var allowedValues = spec.Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(v => v.Trim())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .ToList();

                    if (!allowedValues.Any()) continue;

                    dtos = dtos.Where(p =>
                        p.Specs != null &&
                        p.Specs.ContainsKey(spec.Key) &&
                        allowedValues.Contains(p.Specs[spec.Key], StringComparer.OrdinalIgnoreCase)).ToList();
                }
            }

            return dtos;
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
                    EF.Functions.Like((product.Brand ?? string.Empty).ToLower(), pattern));
            }

            if (!string.IsNullOrWhiteSpace(categoryId))
            {
                var categoryIds = await GetCategoryDescendants(categoryId);
                categoryIds.Add(categoryId);
                productsQuery = productsQuery.Where(product => categoryIds.Contains(product.CategoryId));
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
                    "in" => productsQuery.Where(product => (product.Stock - product.ReservedStock) > 0),
                    "critical" => productsQuery.Where(product => (product.Stock - product.ReservedStock) > 0 && (product.Stock - product.ReservedStock) <= 5),
                    "out" => productsQuery.Where(product => (product.Stock - product.ReservedStock) <= 0),
                    _ => productsQuery
                };
            }

            var totalCount = await productsQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var products = await productsQuery
                .OrderByDescending(product => product.CreatedAt)
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
            if (_cache != null)
            {
                var cachedStr = await _cache.GetStringAsync("featured_products");
                if (!string.IsNullOrEmpty(cachedStr))
                {
                    return JsonSerializer.Deserialize<List<ProductDto>>(cachedStr) ?? new List<ProductDto>();
                }
            }

            var products = await _context.Products
                .AsNoTracking()
                .Where(p => p.IsFeatured && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .ToListAsync();

            var result = _mapper.Map<List<ProductDto>>(products);

            if (_cache != null && result.Any())
            {
                var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) };
                await _cache.SetStringAsync("featured_products", JsonSerializer.Serialize(result), options);
            }

            return result;
        }

        public async Task<ProductDto?> GetByIdAsync(string id)
        {
            if (_cache != null)
            {
                var cachedStr = await _cache.GetStringAsync($"product_{id}");
                if (!string.IsNullOrEmpty(cachedStr))
                {
                    return JsonSerializer.Deserialize<ProductDto>(cachedStr);
                }
            }

            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return null;

            var dto = _mapper.Map<ProductDto>(product);
            if (!string.IsNullOrWhiteSpace(product.CategoryId))
            {
                var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == product.CategoryId);
                dto.CategoryName = category?.Name;
            }

            if (_cache != null)
            {
                var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) };
                await _cache.SetStringAsync($"product_{id}", JsonSerializer.Serialize(dto), options);
            }

            return dto;
        }

        public async Task<ProductDto?> GetBySlugAsync(string slug)
        {
            if (_cache != null)
            {
                var cachedStr = await _cache.GetStringAsync($"product_slug_{slug}");
                if (!string.IsNullOrEmpty(cachedStr))
                {
                    return JsonSerializer.Deserialize<ProductDto>(cachedStr);
                }
            }

            var allDbProducts = await _context.Products.AsNoTracking().ToListAsync();
            var product = allDbProducts.FirstOrDefault(p => UrlHelper.GenerateSlug(p.Brand, p.Name) == slug);
            if (product == null) return null;

            var dto = _mapper.Map<ProductDto>(product);
            if (!string.IsNullOrWhiteSpace(product.CategoryId))
            {
                var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == product.CategoryId);
                dto.CategoryName = category?.Name;
            }

            if (_cache != null)
            {
                var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) };
                await _cache.SetStringAsync($"product_slug_{slug}", JsonSerializer.Serialize(dto), options);
            }

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

            var normalizedKeyword = NormalizeSearchText(keyword);
            var cacheKey = $"search_{normalizedKeyword}";

            if (_cache != null)
            {
                var cachedStr = await _cache.GetStringAsync(cacheKey);
                if (!string.IsNullOrEmpty(cachedStr))
                {
                    return JsonSerializer.Deserialize<List<ProductDto>>(cachedStr) ?? new List<ProductDto>();
                }
            }

            var dbProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive && 
                            (EF.Functions.ILike(p.Name, $"%{keyword}%") || 
                             EF.Functions.ILike(p.Brand, $"%{keyword}%") || 
                             EF.Functions.ILike(p.Description, $"%{keyword}%")))
                .OrderByDescending(p => p.CreatedAt)
                .Take(20)
                .ToListAsync();

            var result = _mapper.Map<List<ProductDto>>(dbProducts);

            if (_cache != null && result.Any())
            {
                var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), options);
            }

            return result;
        }

        public async Task<List<SearchSuggestionDto>> GetSuggestionsAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<SearchSuggestionDto>();

            var dbProducts = await _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive && (EF.Functions.ILike(p.Name, $"%{keyword}%") || EF.Functions.ILike(p.Brand, $"%{keyword}%")))
                .Take(10)
                .ToListAsync();

            var suggestions = new List<SearchSuggestionDto>();

            foreach (var p in dbProducts)
            {
                suggestions.Add(new SearchSuggestionDto
                {
                    Type = "product",
                    Name = p.Name,
                    Id = p.Id,
                    Slug = UrlHelper.GenerateSlug(p.Brand, p.Name),
                    ImageUrl = p.ImageUrls?.FirstOrDefault(),
                    Price = p.Price
                });
            }

            return suggestions;
        }

        public async Task<List<string>> GetBrandsByKeywordAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<string>();

            return await _context.Products
                .AsNoTracking()
                .Where(p => p.IsActive && !string.IsNullOrWhiteSpace(p.Brand) && EF.Functions.ILike(p.Brand, $"%{keyword}%"))
                .Select(p => p.Brand!)
                .Distinct()
                .Take(10)
                .ToListAsync();
        }

        public async Task<SearchFacetResultDto> GetSearchFacetsAsync(string? keyword)
        {
            var query = _context.Products.AsNoTracking().Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(p => EF.Functions.ILike(p.Name, $"%{keyword}%") || 
                                         EF.Functions.ILike(p.Brand, $"%{keyword}%"));
            }

            var products = await query.ToListAsync();
            var facets = new SearchFacetResultDto();

            if (!products.Any())
                return facets;

            var categoryIds = products.Select(p => p.CategoryId).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
            var categories = await _context.Categories.AsNoTracking().Where(c => categoryIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);

            facets.Categories = products
                .GroupBy(p => p.CategoryId)
                .Where(g => !string.IsNullOrEmpty(g.Key) && categories.ContainsKey(g.Key))
                .Select(g => new SearchFacetItemDto
                {
                    Value = categories[g.Key],
                    Count = g.Count()
                })
                .OrderByDescending(f => f.Count)
                .ToList();

            facets.Brands = products
                .GroupBy(p => p.Brand)
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .Select(g => new SearchFacetItemDto
                {
                    Value = g.Key!,
                    Count = g.Count()
                })
                .OrderByDescending(f => f.Count)
                .ToList();

            return facets;
        }
    }
}
