using AutoMapper;
using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Services.Abstract;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Catalog.API.Services.Concrete
{
    public class CategoryQueryService : ICategoryQueryService
    {
        private readonly CatalogDbContext _context;
        private readonly IMapper _mapper;

        public CategoryQueryService(
            CatalogDbContext context,
            IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<List<CategoryDto>> GetAllAsync()
        {
            var categories = await _context.Categories.ToListAsync();
            var attributes = await _context.CategoryAttributes.ToListAsync();
            var products = await _context.Products.ToListAsync();

            var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);
            var directProductCounts = products
                .Where(p => p.IsActive)
                .GroupBy(p => p.CategoryId)
                .ToDictionary(g => g.Key, g => g.Count());

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
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return null;

            var dto = _mapper.Map<CategoryDto>(category);

            // Get attributes
            var attributesList = await _context.CategoryAttributes
                .Where(a => a.CategoryId == id)
                .OrderBy(a => a.DisplayOrder)
                .ThenBy(a => a.Name)
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
            dto.ProductCount = (await _context.Products
                    .Where(p => allDescendantIds.Contains(p.CategoryId))
                    .ToListAsync())
                .Count(p => p.IsActive);

            return dto;
        }

        public async Task<CategoryDto?> GetBySlugAsync(string slug)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Slug == slug);
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
                .ToListAsync();

            return _mapper.Map<List<CategoryAttributeDto>>(attributesList);
        }

        private async Task<List<string>> GetCategoryDescendants(string parentId)
        {
            var children = await _context.Categories
                .Where(c => c.ParentId == parentId)
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
            var specsList = await _context.Products
                .Where(p => categoryIds.Contains(p.CategoryId))
                .Select(p => p.Specs)
                .ToListAsync();

            return specsList
                .Where(specs => specs != null && specs.ContainsKey(attributeName))
                .Select(specs => specs[attributeName])
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .ToList();
        }
    }
}
