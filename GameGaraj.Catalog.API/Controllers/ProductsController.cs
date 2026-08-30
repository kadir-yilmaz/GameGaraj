using GameGaraj.Catalog.API.Data;
using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Exceptions;
using GameGaraj.Catalog.API.Services.Abstract;
using GameGaraj.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameGaraj.Catalog.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IProductQueryService _queries;
        private readonly IProductCommandService _commands;
        private readonly IProductIndexService _productIndexService;
        private readonly CatalogDbContext _dbContext;

        public ProductsController(
            IProductQueryService queries,
            IProductCommandService commands,
            IProductIndexService productIndexService,
            CatalogDbContext dbContext)
        {
            _queries = queries;
            _commands = commands;
            _productIndexService = productIndexService;
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(string? categoryId = null, string? sortBy = null, decimal? minPrice = null, decimal? maxPrice = null, string? brand = null, [FromQuery] Dictionary<string, string>? specs = null)
        {
            var result = await _queries.GetAllAsync(categoryId, sortBy, minPrice, maxPrice, specs, brand);
            return Ok(result);
        }

        [HttpGet("admin")]
        public async Task<IActionResult> GetAdminPage(
            [FromQuery] string? q = null,
            [FromQuery] string? categoryId = null,
            [FromQuery] bool? isFeatured = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] string? stockState = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _queries.GetAdminPageAsync(q, categoryId, isFeatured, isActive, stockState, page, pageSize);
            return Ok(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            var result = await _queries.SearchAsync(q);
            return Ok(result);
        }

        [HttpGet("brands")]
        public async Task<IActionResult> GetBrands([FromQuery] string q)
        {
            var result = await _queries.GetBrandsByKeywordAsync(q);
            return Ok(result);
        }

        [HttpGet("search/suggestions")]
        public async Task<IActionResult> GetSuggestions([FromQuery] string q)
        {
            var result = await _queries.GetSuggestionsAsync(q);
            return Ok(result);
        }

        [HttpGet("search/facets")]
        public async Task<IActionResult> GetSearchFacets([FromQuery] string? q)
        {
            var result = await _queries.GetSearchFacetsAsync(q);
            return Ok(result);
        }

        [HttpGet("featured")]
        public async Task<IActionResult> GetFeatured()
        {
            var result = await _queries.GetFeaturedProductsAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var result = await _queries.GetByIdAsync(id);
            if (result == null)
                return NotFound(new { Message = "Urun bulunamadi." });

            return Ok(result);
        }

        [HttpGet("slug/{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var result = await _queries.GetBySlugAsync(slug);
            if (result == null)
                return NotFound(new { Message = "Urun bulunamadi." });

            return Ok(result);
        }

        [HttpGet("category/{categoryId}")]
        public async Task<IActionResult> GetByCategoryId(string categoryId)
        {
            var result = await _queries.GetByCategoryIdAsync(categoryId);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProductCreateDto dto)
        {
            try
            {
                var result = await _commands.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (CatalogValidationException ex)
            {
                return BadRequest(new { Message = "Urun kaydedilemedi.", Errors = ex.Errors });
            }
        }

        [HttpPut]
        public async Task<IActionResult> Update(ProductUpdateDto dto)
        {
            try
            {
                var result = await _commands.UpdateAsync(dto);
                if (!result)
                    return NotFound(new { Message = "Urun guncellenemedi." });

                return NoContent();
            }
            catch (CatalogValidationException ex)
            {
                return BadRequest(new { Message = "Urun guncellenemedi.", Errors = ex.Errors });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var result = await _commands.DeleteAsync(id);
            if (!result)
                return NotFound(new { Message = "Urun silinemedi." });

            return NoContent();
        }

        [HttpPost("stock/validate")]
        public async Task<IActionResult> ValidateStock([FromBody] StockValidationRequest request)
        {
            if (request == null || request.Items == null || !request.Items.Any())
            {
                return BadRequest(new StockValidationResponse
                {
                    IsValid = false,
                    Errors = new List<string> { "Dogrulanacak urun listesi bos olamaz." }
                });
            }

            var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
            var products = await _dbContext.Products
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            var productDict = products.ToDictionary(p => p.Id, p => p);
            var response = new StockValidationResponse { IsValid = true };

            foreach (var item in request.Items)
            {
                if (!productDict.TryGetValue(item.ProductId, out var product))
                {
                    response.IsValid = false;
                    var err = $"Urun bulunamadi: ID {item.ProductId}";
                    response.Errors.Add(err);
                    response.ItemStatuses.Add(new StockValidationItemStatus
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName ?? item.ProductId,
                        RequestedQuantity = item.Quantity,
                        AvailableStock = 0,
                        IsAvailable = false
                    });
                    continue;
                }

                if (!product.IsActive)
                {
                    response.IsValid = false;
                    var err = $"Urun satisa kapali: {product.Name}";
                    response.Errors.Add(err);
                    response.ItemStatuses.Add(new StockValidationItemStatus
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        RequestedQuantity = item.Quantity,
                        AvailableStock = product.AvailableStock,
                        IsAvailable = false
                    });
                    continue;
                }

                var availableStock = product.AvailableStock;
                var isAvailable = availableStock >= item.Quantity;

                if (!isAvailable)
                {
                    response.IsValid = false;
                    response.Errors.Add($"Yetersiz stok: {product.Name} (Mevcut: {availableStock}, Istenen: {item.Quantity})");
                }

                response.ItemStatuses.Add(new StockValidationItemStatus
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    RequestedQuantity = item.Quantity,
                    AvailableStock = availableStock,
                    IsAvailable = isAvailable
                });
            }

            return Ok(response);
        }
    }
}
