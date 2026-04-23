using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.Catalog.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        // GET: api/products
        [HttpGet]
        public async Task<IActionResult> GetAll(string? categoryId = null, string? sortBy = null, decimal? minPrice = null, decimal? maxPrice = null, [FromQuery] Dictionary<string, string>? specs = null)
        {
            var result = await _productService.GetAllAsync(categoryId, sortBy, minPrice, maxPrice, specs);
            return Ok(result);
        }

        // GET: api/products/search?q=keyword
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            var result = await _productService.SearchAsync(q);
            return Ok(result);
        }

        // GET: api/products/brands?q=keyword
        [HttpGet("brands")]
        public async Task<IActionResult> GetBrands([FromQuery] string q)
        {
            var result = await _productService.GetBrandsByKeywordAsync(q);
            return Ok(result);
        }

        // GET: api/products/featured
        [HttpGet("featured")]
        public async Task<IActionResult> GetFeatured()
        {
            var result = await _productService.GetFeaturedProductsAsync();
            return Ok(result);
        }

        // GET: api/products/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var result = await _productService.GetByIdAsync(id);
            if (result == null)
                return NotFound(new { Message = "Ürün bulunamadı." });

            return Ok(result);
        }

        // GET: api/products/slug/{slug}
        [HttpGet("slug/{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var result = await _productService.GetBySlugAsync(slug);
            if (result == null)
                return NotFound(new { Message = "Ürün bulunamadı." });

            return Ok(result);
        }

        // GET: api/products/category/{categoryId}
        [HttpGet("category/{categoryId}")]
        public async Task<IActionResult> GetByCategoryId(string categoryId)
        {
            var result = await _productService.GetByCategoryIdAsync(categoryId);
            return Ok(result);
        }

        // POST: api/products
        [HttpPost]
        // [Authorize]
        public async Task<IActionResult> Create(ProductCreateDto dto)
        {
            var result = await _productService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        // PUT: api/products
        [HttpPut]
        // [Authorize]
        public async Task<IActionResult> Update(ProductUpdateDto dto)
        {
            var result = await _productService.UpdateAsync(dto);
            if (!result)
                return NotFound(new { Message = "Ürün güncellenemedi." });

            return NoContent();
        }

        // DELETE: api/products/{id}
        [HttpDelete("{id}")]
        // [Authorize]
        public async Task<IActionResult> Delete(string id)
        {
            var result = await _productService.DeleteAsync(id);
            if (!result)
                return NotFound(new { Message = "Ürün silinemedi." });

            return NoContent();
        }

        // DEBUG: api/products/debug/category-test/{categoryId}
        [HttpGet("debug/category-test/{categoryId}")]
        public async Task<IActionResult> DebugCategoryTest(string categoryId)
        {
            var debugInfo = new
            {
                RequestedCategoryId = categoryId,
                Timestamp = DateTime.UtcNow,
                Message = "Check server logs for detailed trace"
            };

            var result = await _productService.GetByCategoryIdAsync(categoryId);

            return Ok(new
            {
                Debug = debugInfo,
                ProductCount = result.Count,
                Products = result
            });
        }
    }
}
