using AutoMapper;
using Elastic.Clients.Elasticsearch;
using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Services.Abstract;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Catalog.API.Services.Concrete
{
    public class CategoryQueryService : ICategoryQueryService
    {
        private readonly CatalogDbContext _context;
        private readonly IMapper _mapper;
        private readonly ElasticsearchClient _elasticClient;
        private readonly ILogger<CategoryQueryService> _logger;

        public CategoryQueryService(
            CatalogDbContext context,
            IMapper mapper,
            ElasticsearchClient elasticClient,
            ILogger<CategoryQueryService> logger)
        {
            _context = context;
            _mapper = mapper;
            _elasticClient = elasticClient;
            _logger = logger;
        }

        public async Task<List<CategoryDto>> GetAllAsync()
        {
            var categories = await _context.Categories.AsNoTracking().ToListAsync();
            var attributes = await _context.CategoryAttributes.AsNoTracking().ToListAsync();

            var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);
            var directProductCounts = await GetDirectProductCountsFromElasticAsync();

            // Build tree structure
            var lookup = categoryDtos.ToDictionary(c => c.Id);
            var roots = new List<CategoryDto>();

            foreach (var cat in categoryDtos)
            {
                // Add attributes
                var catAttributes = attributes
                    .Where(a => a.CategoryId.ToString() == cat.Id)
                    .Select(a => _mapper.Map<CategoryAttributeDto>(a))
                    .ToList();

                // DYNAMIC FILTER FIX: Populate options from actual products
                if (!string.IsNullOrEmpty(cat.Id))
                {
                    var allDescendantIds = await GetCategoryDescendants(cat.Id);
                    allDescendantIds.Add(cat.Id);

                    foreach (var attr in catAttributes)
                    {
                        var dynamicOptions = await GetDistinctValuesForAttributeAsync(allDescendantIds, attr.Name);
                        attr.Options = MergeOptions(attr.Options, dynamicOptions);
                    }
                }

                cat.Attributes = catAttributes;

                if (string.IsNullOrEmpty(cat.ParentId))
                {
                    roots.Add(cat);
                }
                else if (lookup.TryGetValue(cat.ParentId, out var parent))
                {
                    parent.Children.Add(cat);
                }
            }

            foreach (var root in roots)
            {
                SetProductCounts(root, directProductCounts);
            }

            return roots;
        }

        public async Task<CategoryDto?> GetByIdAsync(string id)
        {
            var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (category == null)
                return null;

            var dto = _mapper.Map<CategoryDto>(category);

            // Get attributes
            var attributesList = await _context.CategoryAttributes
                .Where(a => a.CategoryId == id)
                .OrderBy(a => a.DisplayOrder)
                .ThenBy(a => a.Name)
                .AsNoTracking()
                .ToListAsync();
            var dtos = _mapper.Map<List<CategoryAttributeDto>>(attributesList);

            // DYNAMIC FILTER FIX: Populate options from actual products
            var allDescendantIds = await GetCategoryDescendants(id);
            allDescendantIds.Add(id);

            foreach (var attr in dtos)
            {
                var dynamicOptions = await GetDistinctValuesForAttributeAsync(allDescendantIds, attr.Name);
                attr.Options = MergeOptions(attr.Options, dynamicOptions);
            }

            dto.Attributes = dtos;

            var directProductCounts = await GetDirectProductCountsFromElasticAsync();
            dto.ProductCount = allDescendantIds.Sum(categoryId =>
                directProductCounts.TryGetValue(categoryId, out var count) ? count : 0);

            return dto;
        }

        public async Task<CategoryDto?> GetBySlugAsync(string slug)
        {
            var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Slug == slug);
            if (category == null)
                return null;

            return await GetByIdAsync(category.Id); // Re-use GetById logic for attributes and descendants
        }

        public async Task<List<CategoryAttributeDto>> GetAttributesAsync(string categoryId)
        {
            var attributesList = await _context.CategoryAttributes
                .Where(a => a.CategoryId == categoryId)
                .OrderBy(a => a.DisplayOrder)
                .ThenBy(a => a.Name)
                .AsNoTracking()
                .ToListAsync();

            return _mapper.Map<List<CategoryAttributeDto>>(attributesList);
        }

        private async Task<List<string>> GetCategoryDescendants(string parentId)
        {
            var children = await _context.Categories
                .Where(c => c.ParentId == parentId)
                .AsNoTracking()
                .ToListAsync();
            var descendants = new List<string>(children.Select(c => c.Id));

            foreach (var child in children)
            {
                descendants.AddRange(await GetCategoryDescendants(child.Id));
            }

            return descendants;
        }

        private static int SetProductCounts(CategoryDto category, Dictionary<string, int> directProductCounts)
        {
            var count = directProductCounts.TryGetValue(category.Id, out var directCount) ? directCount : 0;

            foreach (var child in category.Children)
            {
                count += SetProductCounts(child, directProductCounts);
            }

            category.ProductCount = count;
            return count;
        }

        private static List<string>? MergeOptions(List<string>? configuredOptions, List<string>? dynamicOptions)
        {
            var merged = (configuredOptions ?? new List<string>())
                .Concat(dynamicOptions ?? new List<string>())
                .Select(option => option?.Trim())
                .Where(option => !string.IsNullOrWhiteSpace(option))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(option => option)
                .Cast<string>()
                .ToList();

            return merged.Count > 0 ? merged : null;
        }

        private async Task<List<string>> GetDistinctValuesForAttributeAsync(List<string> categoryIds, string attributeName)
        {
            var categorySet = categoryIds
                .Where(categoryId => !string.IsNullOrWhiteSpace(categoryId))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!categorySet.Any() || string.IsNullOrWhiteSpace(attributeName))
            {
                return new List<string>();
            }

            var response = await _elasticClient.SearchAsync<ProductSearchDocument>(s => s
                .Index("products")
                .Query(q => q.Bool(b => b.Filter(f => f.Term(t => t.Field("isActive").Value(true)))))
                .Size(1000)
            );

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch category attribute option query failed: {Error}", response.DebugInformation);
                return new List<string>();
            }

            return response.Documents
                .Where(product => categorySet.Contains(product.CategoryId))
                .Select(product => product.Specs)
                .Where(specs => specs != null && specs.TryGetValue(attributeName, out _))
                .Select(specs => specs[attributeName])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToList();
        }

        private async Task<Dictionary<string, int>> GetDirectProductCountsFromElasticAsync()
        {
            var response = await _elasticClient.SearchAsync<ProductSearchDocument>(s => s
                .Index("products")
                .Size(0)
                .Query(q => q.Bool(b => b.Filter(f => f.Term(t => t.Field("isActive").Value(true)))))
                .Aggregations(a => a
                    .Add("categories", agg => agg
                        .Terms(t => t
                            .Field("categoryId")
                            .Size(1000)
                        )
                    )
                )
            );

            if (!response.IsValidResponse)
            {
                _logger.LogWarning("Elasticsearch category count query failed: {Error}", response.DebugInformation);
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            var buckets = response.Aggregations?.GetStringTerms("categories")?.Buckets;

            return buckets?
                .Where(bucket => !string.IsNullOrWhiteSpace(bucket.Key.ToString()))
                .ToDictionary(
                    bucket => bucket.Key.ToString(),
                    bucket => (int)bucket.DocCount,
                    StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
