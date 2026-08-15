using AutoMapper;
using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Exceptions;
using GameGaraj.Catalog.API.Models;
using GameGaraj.Catalog.API.Services.Abstract;
using GameGaraj.Shared.Events;
using GameGaraj.Shared.Helpers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace GameGaraj.Catalog.API.Services.Concrete
{
    public class ProductCommandService : IProductCommandService
    {
        private readonly CatalogDbContext _context;
        private readonly IMapper _mapper;
        private readonly IProductIndexService _productIndexService;
        private readonly ILogger<ProductCommandService> _logger;
        private readonly IDistributedCache? _cache;
        private readonly IPublishEndpoint _publishEndpoint;

        public ProductCommandService(
            CatalogDbContext context,
            IMapper mapper,
            IProductIndexService productIndexService,
            ILogger<ProductCommandService> logger,
            IPublishEndpoint publishEndpoint,
            IDistributedCache? cache = null)
        {
            _context = context;
            _mapper = mapper;
            _productIndexService = productIndexService;
            _logger = logger;
            _publishEndpoint = publishEndpoint;
            _cache = cache;
        }

        public async Task<ProductDto> CreateAsync(ProductCreateDto dto)
        {
            await ValidateProductAsync(dto.Name, dto.Brand, dto.Price, dto.Stock, 0, dto.CategoryId, dto.Specs);

            var product = new Product
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                Brand = dto.Brand,
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

            _context.Products.Add(product);
            EnqueueIndexingJob(product.Id, IndexingJobOperation.Upsert);
            await _context.SaveChangesAsync();
            await TryIndexProductImmediatelyAsync(product);
            await PublishProductCreatedEventAsync(product);
            
            if (_cache != null)
            {
                await _cache.RemoveAsync("featured_products");
            }

            return _mapper.Map<ProductDto>(product);
        }

        public async Task<bool> UpdateAsync(ProductUpdateDto dto)
        {
            var product = await _context.Products.FindAsync(dto.Id);
            if (product == null)
                return false;

            await ValidateProductAsync(dto.Name, dto.Brand, dto.Price, dto.Stock, product.ReservedStock, dto.CategoryId, dto.Specs, dto.Id);

            product.Name = dto.Name;
            product.Brand = dto.Brand;
            product.Description = dto.Description;
            product.Price = dto.Price;
            product.Stock = dto.Stock;
            product.IsActive = dto.IsActive;
            product.IsFeatured = dto.IsFeatured;
            product.ImageUrls = dto.ImageUrls.Take(5).ToList();
            product.CategoryId = dto.CategoryId;
            product.Specs = dto.Specs;
            product.UpdatedAt = DateTime.UtcNow;

            _context.Products.Update(product);
            EnqueueIndexingJob(product.Id, IndexingJobOperation.Upsert);
            var updated = await _context.SaveChangesAsync() > 0;
            if (updated)
            {
                await TryIndexProductImmediatelyAsync(product);
                await PublishProductUpdatedEventAsync(product);
                if (_cache != null)
                {
                    await _cache.RemoveAsync($"product_{product.Id}");
                    await _cache.RemoveAsync("featured_products");
                }
            }

            return updated;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return false;

            _context.Products.Remove(product);
            EnqueueIndexingJob(id, IndexingJobOperation.Delete);
            var deleted = await _context.SaveChangesAsync() > 0;
            if (deleted)
            {
                await TryDeleteProductFromIndexImmediatelyAsync(id);
                await PublishProductDeletedEventAsync(id);
                if (_cache != null)
                {
                    await _cache.RemoveAsync($"product_{id}");
                    await _cache.RemoveAsync("featured_products");
                }
            }

            return deleted;
        }

        private async Task PublishProductCreatedEventAsync(Product product)
        {
            try
            {
                var category = !string.IsNullOrWhiteSpace(product.CategoryId)
                    ? await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == product.CategoryId)
                    : null;

                await _publishEndpoint.Publish(new ProductCreatedForSearch
                {
                    Id = product.Id,
                    Name = product.Name,
                    Brand = product.Brand,
                    Slug = UrlHelper.GenerateSlug(product.Brand, product.Name),
                    Description = product.Description,
                    Price = product.Price,
                    Stock = product.Stock,
                    ReservedStock = product.ReservedStock,
                    IsActive = product.IsActive,
                    IsFeatured = product.IsFeatured,
                    ImageUrls = product.ImageUrls,
                    CategoryId = product.CategoryId,
                    CategoryName = category?.Name ?? string.Empty,
                    CategorySlug = UrlHelper.GenerateSlug(category?.Name),
                    Specs = product.Specs
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish ProductCreatedForSearch event for {ProductId}", product.Id);
            }
        }

        private async Task PublishProductUpdatedEventAsync(Product product)
        {
            try
            {
                var category = !string.IsNullOrWhiteSpace(product.CategoryId)
                    ? await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == product.CategoryId)
                    : null;

                await _publishEndpoint.Publish(new ProductUpdatedForSearch
                {
                    Id = product.Id,
                    Name = product.Name,
                    Brand = product.Brand,
                    Slug = UrlHelper.GenerateSlug(product.Brand, product.Name),
                    Description = product.Description,
                    Price = product.Price,
                    Stock = product.Stock,
                    ReservedStock = product.ReservedStock,
                    IsActive = product.IsActive,
                    IsFeatured = product.IsFeatured,
                    ImageUrls = product.ImageUrls,
                    CategoryId = product.CategoryId,
                    CategoryName = category?.Name ?? string.Empty,
                    CategorySlug = UrlHelper.GenerateSlug(category?.Name),
                    Specs = product.Specs
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish ProductUpdatedForSearch event for {ProductId}", product.Id);
            }
        }

        private async Task PublishProductDeletedEventAsync(string productId)
        {
            try
            {
                await _publishEndpoint.Publish(new ProductDeletedForSearch
                {
                    Id = productId
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish ProductDeletedForSearch event for {ProductId}", productId);
            }
        }

        private async Task TryIndexProductImmediatelyAsync(Product product)
        {
            try
            {
                await _productIndexService.IndexAsync(product);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Immediate product indexing failed for {ProductId}. Queued indexing job will retry.", product.Id);
            }
        }

        private async Task TryDeleteProductFromIndexImmediatelyAsync(string productId)
        {
            try
            {
                await _productIndexService.DeleteAsync(productId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Immediate product index delete failed for {ProductId}. Queued indexing job will retry.", productId);
            }
        }

        private void EnqueueIndexingJob(string entityId, string operation)
        {
            _context.IndexingJobs.Add(new IndexingJob
            {
                Id = Guid.NewGuid().ToString(),
                EntityType = "Product",
                EntityId = entityId,
                Operation = operation,
                Status = IndexingJobStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }

        private async Task ValidateProductAsync(
            string name,
            string brand,
            decimal price,
            int stock,
            int reservedStock,
            string categoryId,
            Dictionary<string, string>? specs)
            => await ValidateProductAsync(name, brand, price, stock, reservedStock, categoryId, specs, null);

        private async Task ValidateProductAsync(
            string name,
            string brand,
            decimal price,
            int stock,
            int reservedStock,
            string categoryId,
            Dictionary<string, string>? specs,
            string? currentProductId)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(name))
                errors.Add("Ürün adı zorunludur.");

            if (string.IsNullOrWhiteSpace(brand))
                errors.Add("Marka zorunludur.");

            if (price < 0)
                errors.Add("Ürün fiyatı negatif olamaz.");

            if (stock < 0)
                errors.Add("Stok negatif olamaz.");

            if (reservedStock < 0)
                errors.Add("Rezerve stok negatif olamaz.");

            if (reservedStock > stock)
                errors.Add("Rezerve stok toplam stoktan fazla olamaz.");

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(brand))
            {
                var lowerBrand = brand.ToLower();
                var lowerName = name.ToLower();
                var nameExists = await _context.Products.AnyAsync(product =>
                    product.Brand.ToLower() == lowerBrand && product.Name.ToLower() == lowerName && product.Id != currentProductId);

                if (nameExists)
                    errors.Add("Aynı marka ve ürün adına sahip başka bir ürün zaten var.");
            }

            if (string.IsNullOrWhiteSpace(categoryId))
            {
                errors.Add("Kategori zorunludur.");
                throwIfAny(errors);
                return;
            }

            var categoryExists = await _context.Categories.AnyAsync(category => category.Id == categoryId);
            if (!categoryExists)
            {
                errors.Add("Seçilen kategori bulunamadı.");
                throwIfAny(errors);
                return;
            }

            var allowedCategoryIds = await GetCategoryAndAncestorIdsAsync(categoryId);
            var attributes = await _context.CategoryAttributes
                .Where(attribute => allowedCategoryIds.Contains(attribute.CategoryId))
                .ToListAsync();

            var attributeLookup = attributes.ToDictionary(attribute => attribute.Name, StringComparer.OrdinalIgnoreCase);
            specs ??= new Dictionary<string, string>();

            foreach (var key in specs.Keys.Where(key => !string.IsNullOrWhiteSpace(key)))
            {
                if (!attributeLookup.ContainsKey(key))
                {
                    errors.Add($"'{key}' bu kategori için tanımlı bir özellik değil.");
                }
            }

            foreach (var attribute in attributes.Where(attribute => attribute.IsRequired))
            {
                if (!specs.TryGetValue(attribute.Name, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    errors.Add($"'{attribute.DisplayName}' zorunlu bir özelliktir.");
                }
            }

            foreach (var spec in specs.Where(spec => !string.IsNullOrWhiteSpace(spec.Key)))
            {
                if (!attributeLookup.TryGetValue(spec.Key, out var attribute))
                    continue;

                if (attribute.Type == AttributeType.Dropdown &&
                    attribute.Options is { Count: > 0 } &&
                    !attribute.Options.Contains(spec.Value, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"'{attribute.DisplayName}' için '{spec.Value}' geçerli bir değer değil.");
                }
            }

            throwIfAny(errors);

            static void throwIfAny(List<string> validationErrors)
            {
                if (validationErrors.Any())
                    throw new CatalogValidationException(validationErrors);
            }
        }

        private async Task<List<string>> GetCategoryAndAncestorIdsAsync(string categoryId)
        {
            var result = new List<string>();
            var currentId = categoryId;

            while (!string.IsNullOrWhiteSpace(currentId))
            {
                var category = await _context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == currentId);

                if (category == null)
                    break;

                result.Add(category.Id);
                currentId = category.ParentId;
            }

            return result;
        }
    }
}
