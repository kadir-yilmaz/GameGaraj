using AutoMapper;
using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Exceptions;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Services.Abstract;
using GameGaraj.Shared.Helpers;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Catalog.API.Services.Concrete
{
    public class CategoryCommandService : ICategoryCommandService
    {
        private readonly CatalogDbContext _context;
        private readonly IMapper _mapper;

        public CategoryCommandService(CatalogDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<CategoryDto> CreateAsync(CategoryCreateDto dto)
        {
            await ValidateCategoryAsync(dto.Name, dto.ParentId);

            var now = DateTime.UtcNow;
            var category = new Category
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                Slug = UrlHelper.GenerateSlug(dto.Name),
                ParentId = dto.ParentId,
                IsShowOnHome = dto.IsShowOnHome,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return _mapper.Map<CategoryDto>(category);
        }

        public async Task<CategoryDto?> UpdateAsync(string id, CategoryCreateDto dto)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return null;

            await ValidateCategoryAsync(dto.Name, dto.ParentId, id);

            category.Name = dto.Name;
            category.Slug = UrlHelper.GenerateSlug(dto.Name);
            category.ParentId = dto.ParentId;
            category.IsShowOnHome = dto.IsShowOnHome;
            category.UpdatedAt = DateTime.UtcNow;

            _context.Categories.Update(category);
            await _context.SaveChangesAsync();
            return _mapper.Map<CategoryDto>(category);
        }

        public async Task<bool> ToggleShowOnHomeAsync(string id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return false;

            category.IsShowOnHome = !category.IsShowOnHome;
            category.UpdatedAt = DateTime.UtcNow;

            _context.Categories.Update(category);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<CategoryAttributeDto> AddAttributeAsync(string categoryId, CategoryAttributeCreateDto dto)
        {
            var type = await ValidateAttributeAsync(categoryId, dto);

            var attribute = new CategoryAttribute
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                DisplayName = dto.DisplayName,
                Type = type,
                Options = dto.Options,
                CategoryId = categoryId,
                CreatedAt = DateTime.UtcNow
            };

            _context.CategoryAttributes.Add(attribute);
            await _context.SaveChangesAsync();
            return _mapper.Map<CategoryAttributeDto>(attribute);
        }

        public async Task<CategoryAttributeDto?> UpdateAttributeAsync(string categoryId, string attributeId, CategoryAttributeCreateDto dto)
        {
            var existing = await _context.CategoryAttributes.FindAsync(attributeId);
            if (existing == null || existing.CategoryId != categoryId)
                return null;

            var type = await ValidateAttributeAsync(categoryId, dto, attributeId);

            var previousName = existing.Name;
            existing.Name = dto.Name;
            existing.DisplayName = dto.DisplayName;
            existing.Type = type;
            existing.Options = dto.Options;

            _context.CategoryAttributes.Update(existing);

            if (!string.Equals(previousName, existing.Name, StringComparison.OrdinalIgnoreCase))
            {
                await RenameSpecOnProductsAsync(categoryId, previousName, existing.Name);
            }

            await _context.SaveChangesAsync();
            return _mapper.Map<CategoryAttributeDto>(existing);
        }

        public async Task<bool> DeleteAttributeAsync(string categoryId, string attributeId)
        {
            var existing = await _context.CategoryAttributes.FindAsync(attributeId);
            if (existing == null || existing.CategoryId != categoryId)
                return false;

            await RemoveSpecFromProductsAsync(categoryId, existing.Name);

            _context.CategoryAttributes.Remove(existing);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            if (id == "uncategorized")
                return false;

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return false;

            var products = await _context.Products.Where(p => p.CategoryId == id).ToListAsync();
            foreach (var p in products)
            {
                p.CategoryId = "uncategorized";
            }

            var children = await _context.Categories.Where(c => c.ParentId == id).ToListAsync();
            foreach (var child in children)
            {
                child.ParentId = null;
            }

            _context.Categories.Remove(category);
            return await _context.SaveChangesAsync() > 0;
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

        private async Task RemoveSpecFromProductsAsync(string categoryId, string specName)
        {
            if (string.IsNullOrWhiteSpace(specName))
                return;

            var categoryIds = await GetCategoryDescendants(categoryId);
            categoryIds.Add(categoryId);

            var products = await _context.Products
                .Where(p => categoryIds.Contains(p.CategoryId))
                .ToListAsync();

            foreach (var product in products)
            {
                if (product.Specs == null || !product.Specs.Remove(specName))
                    continue;

                product.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        private async Task ValidateCategoryAsync(string name, string? parentId, string? currentCategoryId = null)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(name))
                errors.Add("Kategori adı zorunludur.");
            else
            {
                var slug = UrlHelper.GenerateSlug(name);
                var slugExists = await _context.Categories.AnyAsync(category =>
                    category.Slug == slug && category.Id != currentCategoryId);

                if (slugExists)
                    errors.Add("Aynı ada sahip başka bir kategori zaten var.");
            }

            if (!string.IsNullOrWhiteSpace(parentId))
            {
                var parentExists = await _context.Categories.AnyAsync(category => category.Id == parentId);
                if (!parentExists)
                {
                    errors.Add("Üst kategori bulunamadı.");
                }

                if (!string.IsNullOrWhiteSpace(currentCategoryId))
                {
                    if (parentId == currentCategoryId)
                    {
                        errors.Add("Kategori kendi üst kategorisi olamaz.");
                    }
                    else
                    {
                        var descendants = await GetCategoryDescendants(currentCategoryId);
                        if (descendants.Contains(parentId))
                        {
                            errors.Add("Kategori kendi alt kategorisinin altına taşınamaz.");
                        }
                    }
                }
            }

            if (errors.Any())
                throw new CatalogValidationException(errors);
        }

        private async Task<AttributeType> ValidateAttributeAsync(
            string categoryId,
            CategoryAttributeCreateDto dto,
            string? currentAttributeId = null)
        {
            var errors = new List<string>();

            if (!await _context.Categories.AnyAsync(category => category.Id == categoryId))
                errors.Add("Kategori bulunamadı.");

            if (string.IsNullOrWhiteSpace(dto.Name))
                errors.Add("Özellik kodu zorunludur.");

            if (string.IsNullOrWhiteSpace(dto.DisplayName))
                errors.Add("Özellik görünen adı zorunludur.");

            if (!Enum.TryParse<AttributeType>(dto.Type, true, out var type))
            {
                errors.Add("Özellik tipi geçersiz.");
                type = AttributeType.Text;
            }

            if (type == AttributeType.Dropdown)
            {
                dto.Options = dto.Options?
                    .Where(option => !string.IsNullOrWhiteSpace(option))
                    .Select(option => option.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (dto.Options == null || dto.Options.Count == 0)
                    errors.Add("Dropdown özellik için en az bir seçenek girilmelidir.");
            }
            else
            {
                dto.Options = null;
            }

            var duplicateExists = await _context.CategoryAttributes.AnyAsync(attribute =>
                attribute.CategoryId == categoryId &&
                attribute.Name == dto.Name &&
                attribute.Id != currentAttributeId);

            if (duplicateExists)
                errors.Add("Bu kategoride aynı özellik kodu zaten var.");

            if (errors.Any())
                throw new CatalogValidationException(errors);

            dto.Name = dto.Name.Trim();
            dto.DisplayName = dto.DisplayName.Trim();

            return type;
        }

        private async Task RenameSpecOnProductsAsync(string categoryId, string previousName, string nextName)
        {
            if (string.IsNullOrWhiteSpace(previousName) || string.IsNullOrWhiteSpace(nextName))
                return;

            var categoryIds = await GetCategoryDescendants(categoryId);
            categoryIds.Add(categoryId);

            var products = await _context.Products
                .Where(product => categoryIds.Contains(product.CategoryId))
                .ToListAsync();

            foreach (var product in products)
            {
                if (product.Specs == null || !product.Specs.TryGetValue(previousName, out var value))
                    continue;

                product.Specs.Remove(previousName);
                product.Specs[nextName] = value;
                product.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
