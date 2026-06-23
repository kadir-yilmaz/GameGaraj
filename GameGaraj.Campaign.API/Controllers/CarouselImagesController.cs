using GameGaraj.Campaign.API.Models;
using GameGaraj.Campaign.API.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.Campaign.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CarouselImagesController : ControllerBase
    {
        private readonly ICarouselImageService _carouselService;

        public CarouselImagesController(ICarouselImageService carouselService)
        {
            _carouselService = carouselService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var images = await _carouselService.GetAllAsync();
            return Ok(images);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var image = await _carouselService.GetByIdAsync(id);
            if (image == null)
                return NotFound($"ID: {id} ile carousel görseli bulunamadı.");

            return Ok(image);
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] CarouselImage image)
        {
            var result = await _carouselService.SaveAsync(image);
            if (!result)
                return BadRequest("Carousel görseli kaydedilemedi.");

            return Created("", image);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _carouselService.DeleteAsync(id);
            if (!result)
                return NotFound($"ID: {id} ile carousel görseli bulunamadı veya silinemedi.");

            return NoContent();
        }
    }
}
