using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GameGaraj.Discount.API.Services.Abstract;

namespace GameGaraj.Discount.API.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class DiscountsController : ControllerBase
    {
        private readonly IDiscountService _discountService;

        public DiscountsController(IDiscountService discountService)
        {
            _discountService = discountService;
        }

        /// <summary>
        /// Tüm indirimleri getirir
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var discounts = await _discountService.GetAllAsync();
            return Ok(discounts);
        }

        /// <summary>
        /// UserId'ye göre indirimleri getirir
        /// </summary>
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetAllByUserId(string userId)
        {
            var discounts = await _discountService.GetAllByUserIdAsync(userId);
            return Ok(discounts);
        }

        /// <summary>
        /// ID'ye göre indirim getirir
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var discount = await _discountService.GetByIdAsync(id);
            
            if (discount == null)
                return NotFound($"ID: {id} ile indirim bulunamadı.");
            
            return Ok(discount);
        }

        /// <summary>
        /// Kod ve UserId'ye göre indirim getirir
        /// </summary>
        [HttpGet("code/{code}/user/{userId}")]
        public async Task<IActionResult> GetByCodeAndUserId(string code, string userId)
        {
            var discount = await _discountService.GetByCodeAndUserIdAsync(code, userId);
            
            if (discount == null)
                return NotFound($"Kod: {code} ve UserId: {userId} ile indirim bulunamadı.");
            
            return Ok(discount);
        }

        /// <summary>
        /// Sadece koda göre indirim getirir (Global kullanım)
        /// </summary>
        [HttpGet("code/{code}")]
        public async Task<IActionResult> GetByCode(string code)
        {
            var discount = await _discountService.GetByCodeAsync(code);

            if (discount == null)
                return NotFound($"Kod: {code} ile indirim bulunamadı.");

            return Ok(discount);
        }

        /// <summary>
        /// Yeni indirim oluşturur
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Save([FromBody] Models.Discount discount)
        {
            var result = await _discountService.SaveAsync(discount);
            
            if (!result)
                return BadRequest("İndirim kaydedilemedi.");
            
            return Created("", discount);
        }

        /// <summary>
        /// Mevcut indirimi günceller
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] Models.Discount discount)
        {
            var result = await _discountService.UpdateAsync(discount);
            
            if (!result)
                return NotFound($"ID: {discount.Id} ile indirim bulunamadı veya güncellenemedi.");
            
            return NoContent();
        }

        /// <summary>
        /// ID'ye göre indirim siler
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _discountService.DeleteAsync(id);
            
            if (!result)
                return NotFound($"ID: {id} ile indirim bulunamadı veya silinemedi.");
            
            return NoContent();
        }
    }
}
