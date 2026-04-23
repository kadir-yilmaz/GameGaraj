using AutoMapper;
using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Repositories.Abstract;
using GameGaraj.Catalog.API.Services.Abstract;
using GameGaraj.Shared.Helpers;

namespace GameGaraj.Catalog.API.Services.Concrete
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IProductRepository _productRepository;
        private readonly IAttributeRepository _attributeRepository;
        private readonly IMapper _mapper;

        public CategoryService(
            ICategoryRepository categoryRepository,
            IProductRepository productRepository,
            IAttributeRepository attributeRepository,
            IMapper mapper)
        {
            _categoryRepository = categoryRepository;
            _productRepository = productRepository;
            _attributeRepository = attributeRepository;
            _mapper = mapper;
        }

        public async Task<List<CategoryDto>> GetAllAsync()
        {
            var categories = await _categoryRepository.GetAllAsync();
            var attributes = await _attributeRepository.GetAllAsync();

            var categoryDtos = _mapper.Map<List<CategoryDto>>(categories);

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
                        var dynamicOptions = await _productRepository.GetDistinctValuesForAttributeAsync(allDescendantIds, attr.Name);
                        if (dynamicOptions.Any())
                        {
                            attr.Options = dynamicOptions;
                        }
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

            return roots;
        }

        public async Task<CategoryDto?> GetByIdAsync(string id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
                return null;

            var dto = _mapper.Map<CategoryDto>(category);

            // Get attributes
            var attributesList = await _attributeRepository.GetByCategoryIdAsync(id);
            var dtos = _mapper.Map<List<CategoryAttributeDto>>(attributesList);

            // DYNAMIC FILTER FIX: Populate options from actual products
            var allDescendantIds = await GetCategoryDescendants(id);
            allDescendantIds.Add(id);

            foreach (var attr in dtos)
            {
                var dynamicOptions = await _productRepository.GetDistinctValuesForAttributeAsync(allDescendantIds, attr.Name);
                if (dynamicOptions.Any())
                {
                    attr.Options = dynamicOptions;
                }
            }

            dto.Attributes = dtos;

            return dto;
        }

        public async Task<CategoryDto?> GetBySlugAsync(string slug)
        {
            var category = await _categoryRepository.GetBySlugAsync(slug);
            if (category == null)
                return null;

            return await GetByIdAsync(category.Id); // Re-use GetById logic for attributes and descendants
        }

        public async Task<CategoryDto> CreateAsync(CategoryCreateDto dto)
        {
            var now = DateTime.UtcNow;
            var category = new Category
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                Slug = UrlHelper.GenerateSlug(dto.Name),
                ParentId = dto.ParentId,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _categoryRepository.CreateAsync(category);
            return _mapper.Map<CategoryDto>(category);
        }

        public async Task<CategoryDto?> UpdateAsync(string id, CategoryCreateDto dto)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
                return null;

            category.Name = dto.Name;
            category.Slug = UrlHelper.GenerateSlug(dto.Name);
            category.ParentId = dto.ParentId;
            category.UpdatedAt = DateTime.UtcNow;

            await _categoryRepository.UpdateAsync(category);
            return _mapper.Map<CategoryDto>(category);
        }

        public async Task<List<CategoryAttributeDto>> GetAttributesAsync(string categoryId)
        {
            var attributesList = await _attributeRepository.GetByCategoryIdAsync(categoryId);

            return _mapper.Map<List<CategoryAttributeDto>>(attributesList);
        }

        public async Task<CategoryAttributeDto> AddAttributeAsync(string categoryId, CategoryAttributeCreateDto dto)
        {
            var attribute = new CategoryAttribute
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                DisplayName = dto.DisplayName,
                Type = Enum.TryParse<AttributeType>(dto.Type, true, out var type) ? type : AttributeType.Text,
                Options = dto.Options,
                CategoryId = categoryId,
                CreatedAt = DateTime.UtcNow
            };

            await _attributeRepository.CreateAsync(attribute);
            return _mapper.Map<CategoryAttributeDto>(attribute);
        }

        public async Task<CategoryAttributeDto?> UpdateAttributeAsync(string categoryId, string attributeId, CategoryAttributeCreateDto dto)
        {
            var existing = await _attributeRepository.GetByIdAsync(attributeId);
            if (existing == null || existing.CategoryId != categoryId)
                return null;

            existing.Name = dto.Name;
            existing.DisplayName = dto.DisplayName;
            existing.Type = Enum.TryParse<AttributeType>(dto.Type, true, out var type) ? type : AttributeType.Text;
            existing.Options = dto.Options;

            var updated = await _attributeRepository.UpdateAsync(attributeId, existing);
            return updated != null ? _mapper.Map<CategoryAttributeDto>(updated) : null;
        }

        public async Task<bool> DeleteAttributeAsync(string categoryId, string attributeId)
        {
            var existing = await _attributeRepository.GetByIdAsync(attributeId);
            if (existing == null || existing.CategoryId != categoryId)
                return false;

            return await _attributeRepository.DeleteAsync(attributeId);
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
    }
}
