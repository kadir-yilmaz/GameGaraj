using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.Catalog.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        // GET: api/categories
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _categoryService.GetAllAsync();
            return Ok(result);
        }

        // GET: api/categories/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var result = await _categoryService.GetByIdAsync(id);
            if (result == null)
                return NotFound(new { Message = "Kategori bulunamadı." });

            return Ok(result);
        }

        // POST: api/categories
        [HttpPost]
        // [Authorize]
        public async Task<IActionResult> Create(CategoryCreateDto dto)
        {
            var result = await _categoryService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        // PUT: api/categories/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, CategoryCreateDto dto)
        {
            var result = await _categoryService.UpdateAsync(id, dto);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        // GET: api/categories/{id}/attributes
        [HttpGet("{id}/attributes")]
        public async Task<IActionResult> GetAttributes(string id)
        {
            var result = await _categoryService.GetAttributesAsync(id);
            return Ok(result);
        }

        // POST: api/categories/{id}/attributes
        [HttpPost("{id}/attributes")]
        // [Authorize]
        public async Task<IActionResult> AddAttribute(string id, CategoryAttributeCreateDto dto)
        {
            var result = await _categoryService.AddAttributeAsync(id, dto);
            return Created("", result);
        }

        // PUT: api/categories/{id}/attributes/{attributeId}
        [HttpPut("{id}/attributes/{attributeId}")]
        public async Task<IActionResult> UpdateAttribute(string id, string attributeId, CategoryAttributeCreateDto dto)
        {
            var result = await _categoryService.UpdateAttributeAsync(id, attributeId, dto);
            if (result == null)
                return NotFound();

            return Ok(result);
        }

        // DELETE: api/categories/{id}/attributes/{attributeId}
        [HttpDelete("{id}/attributes/{attributeId}")]
        public async Task<IActionResult> DeleteAttribute(string id, string attributeId)
        {
            var result = await _categoryService.DeleteAttributeAsync(id, attributeId);
            if (!result)
                return NotFound();

            return NoContent();
        }
    }
}
