using GameGaraj.Campaign.API.Models;
using GameGaraj.Campaign.API.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.Campaign.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CouponRewardRulesController : ControllerBase
    {
        private readonly ICouponRewardService _rewardService;

        public CouponRewardRulesController(ICouponRewardService rewardService)
        {
            _rewardService = rewardService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rules = await _rewardService.GetAllAsync();
            return Ok(rules);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var rule = await _rewardService.GetByIdAsync(id);
            if (rule == null)
                return NotFound($"ID: {id} ile ödül kuralı bulunamadı.");

            return Ok(rule);
        }

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] CouponRewardRule rule)
        {
            var result = await _rewardService.SaveAsync(rule);
            if (!result)
                return BadRequest("Ödül kuralı kaydedilemedi.");

            return Created("", rule);
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] CouponRewardRule rule)
        {
            var result = await _rewardService.UpdateAsync(rule);
            if (!result)
                return NotFound($"ID: {rule.Id} ile ödül kuralı bulunamadı veya güncellenemedi.");

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _rewardService.DeleteAsync(id);
            if (!result)
                return NotFound($"ID: {id} ile ödül kuralı bulunamadı veya silinemedi.");

            return NoContent();
        }
    }
}
