using GameGaraj.Campaign.API.Models;
using GameGaraj.Campaign.API.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

using GameGaraj.Shared.Observability.Metrics;

namespace GameGaraj.Campaign.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CouponsController : ControllerBase
    {
        private readonly ICouponService _couponService;
        private readonly CampaignMetrics _metrics;

        public CouponsController(
            ICouponService couponService,
            CampaignMetrics metrics)
        {
            _couponService = couponService;
            _metrics = metrics;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var coupons = await _couponService.GetAllAsync();
            return Ok(coupons);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var coupon = await _couponService.GetByIdAsync(id);
            if (coupon == null)
                return NotFound($"ID: {id} ile kupon bulunamadı.");

            return Ok(coupon);
        }

        [HttpGet("code/{code}")]
        public async Task<IActionResult> GetByCode(string code)
        {
            var coupon = await _couponService.GetByCodeAsync(code);
            if (coupon == null)
                return NotFound($"Kod: {code} ile kupon bulunamadı.");

            return Ok(coupon);
        }

        [HttpGet("public")]
        public async Task<IActionResult> GetPublic()
        {
            var coupons = await _couponService.GetPublicCouponsAsync();
            return Ok(coupons);
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetByUserId(string userId)
        {
            var coupons = await _couponService.GetByUserIdAsync(userId);
            return Ok(coupons);
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] Coupon coupon)
        {
            var result = await _couponService.SaveAsync(coupon);
            if (!result)
                return BadRequest("Kupon kaydedilemedi.");

            _metrics.CouponCreated();
            return Created("", coupon);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] Coupon coupon)
        {
            var result = await _couponService.UpdateAsync(coupon);
            if (!result)
                return NotFound($"ID: {coupon.Id} ile kupon bulunamadı veya güncellenemedi.");

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _couponService.DeleteAsync(id);
            if (!result)
                return NotFound($"ID: {id} ile kupon bulunamadı veya silinemedi.");

            return NoContent();
        }

        [HttpPost("{id}/markused")]
        public async Task<IActionResult> MarkAsUsed(int id)
        {
            var result = await _couponService.MarkAsUsedAsync(id);
            if (!result)
                return NotFound($"ID: {id} ile kupon bulunamadı.");

            _metrics.CouponUsed();
            return NoContent();
        }

        [HttpPut("use/{code}")]
        public async Task<IActionResult> MarkAsUsedByCode(string code)
        {
            var coupon = await _couponService.GetByCodeAsync(code);
            if (coupon == null)
                return NotFound($"Kod: {code} ile kupon bulunamadı.");

            var result = await _couponService.MarkAsUsedAsync(coupon.Id);
            if (!result)
                return BadRequest("Kupon kullanıldı olarak işaretlenemedi.");

            _metrics.CouponUsed();
            return NoContent();
        }
    }
}
