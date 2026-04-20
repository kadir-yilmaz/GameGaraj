using GameGaraj.Campaign.API.Models;
using GameGaraj.Campaign.API.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.Campaign.API.Controllers
{
    /// <summary>
    /// Kampanya kuralları CRUD controller'ı.
    /// Admin panelden kuralları yönetmek için kullanılır.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class CampaignRulesController : ControllerBase
    {
        private readonly ICampaignRuleService _ruleService;

        public CampaignRulesController(ICampaignRuleService ruleService)
        {
            _ruleService = ruleService;
        }

        /// <summary>
        /// Tüm kampanya kurallarını getirir
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var rules = await _ruleService.GetAllAsync();
            return Ok(rules);
        }

        /// <summary>
        /// ID'ye göre kampanya kuralı getirir
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var rule = await _ruleService.GetByIdAsync(id);
            if (rule == null)
                return NotFound($"ID: {id} ile kampanya kuralı bulunamadı.");

            return Ok(rule);
        }

        /// <summary>
        /// Yeni kampanya kuralı oluşturur
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Save([FromBody] CampaignRule rule)
        {
            var result = await _ruleService.SaveAsync(rule);
            if (!result)
                return BadRequest("Kampanya kuralı kaydedilemedi.");

            return Created("", rule);
        }

        /// <summary>
        /// Mevcut kampanya kuralını günceller
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> Update([FromBody] CampaignRule rule)
        {
            var result = await _ruleService.UpdateAsync(rule);
            if (!result)
                return NotFound($"ID: {rule.Id} ile kampanya kuralı bulunamadı veya güncellenemedi.");

            return NoContent();
        }

        /// <summary>
        /// ID'ye göre kampanya kuralı siler
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _ruleService.DeleteAsync(id);
            if (!result)
                return NotFound($"ID: {id} ile kampanya kuralı bulunamadı veya silinemedi.");

            return NoContent();
        }
    }
}
