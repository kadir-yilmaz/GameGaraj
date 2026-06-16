using GameGaraj.Catalog.API.Dtos;
using GameGaraj.Catalog.API.Exceptions;
using GameGaraj.Catalog.API.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.Catalog.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryQueryService _queries;
        private readonly ICategoryCommandService _commands;

        public CategoriesController(ICategoryQueryService queries, ICategoryCommandService commands)
        {
            _queries = queries;
            _commands = commands;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _queries.GetAllAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var result = await _queries.GetByIdAsync(id);
            if (result == null)
                return NotFound(new { Message = "Kategori bulunamadi." });

            return Ok(result);
        }

        [HttpGet("slug/{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var result = await _queries.GetBySlugAsync(slug);
            if (result == null)
                return NotFound(new { Message = "Kategori bulunamadi." });

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CategoryCreateDto dto)
        {
            try
            {
                var result = await _commands.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
            }
            catch (CatalogValidationException ex)
            {
                return BadRequest(new { Message = "Kategori kaydedilemedi.", Errors = ex.Errors });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, CategoryCreateDto dto)
        {
            try
            {
                var result = await _commands.UpdateAsync(id, dto);
                if (result == null)
                    return NotFound();

                return Ok(result);
            }
            catch (CatalogValidationException ex)
            {
                return BadRequest(new { Message = "Kategori guncellenemedi.", Errors = ex.Errors });
            }
        }

        [HttpGet("{id}/attributes")]
        public async Task<IActionResult> GetAttributes(string id)
        {
            var result = await _queries.GetAttributesAsync(id);
            return Ok(result);
        }

        [HttpPost("{id}/attributes")]
        public async Task<IActionResult> AddAttribute(string id, CategoryAttributeCreateDto dto)
        {
            try
            {
                var result = await _commands.AddAttributeAsync(id, dto);
                return Created("", result);
            }
            catch (CatalogValidationException ex)
            {
                return BadRequest(new { Message = "Ozellik kaydedilemedi.", Errors = ex.Errors });
            }
        }

        [HttpPut("{id}/attributes/{attributeId}")]
        public async Task<IActionResult> UpdateAttribute(string id, string attributeId, CategoryAttributeCreateDto dto)
        {
            try
            {
                var result = await _commands.UpdateAttributeAsync(id, attributeId, dto);
                if (result == null)
                    return NotFound();

                return Ok(result);
            }
            catch (CatalogValidationException ex)
            {
                return BadRequest(new { Message = "Ozellik guncellenemedi.", Errors = ex.Errors });
            }
        }

        [HttpDelete("{id}/attributes/{attributeId}")]
        public async Task<IActionResult> DeleteAttribute(string id, string attributeId)
        {
            var result = await _commands.DeleteAttributeAsync(id, attributeId);
            if (!result)
                return NotFound();

            return NoContent();
        }
    }
}
