using GameGaraj.Campaign.API.Models;
using GameGaraj.Campaign.API.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.Campaign.API.Controllers
{
    /// <summary>
    /// Sepet bilgisine göre indirim hesaplayan endpoint.
    /// WebUI bu endpoint'i çağırarak müşteriye en avantajlı indirimi gösterir.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class CampaignCalculateController : ControllerBase
    {
        private readonly ICampaignCalculationService _calculationService;

        public CampaignCalculateController(ICampaignCalculationService calculationService)
        {
            _calculationService = calculationService;
        }

        /// <summary>
        /// Sepet bilgisi gönderilir, en avantajlı indirim kuralı hesaplanıp döner.
        /// Best Single Discount stratejisi uygulanır.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Calculate([FromBody] CalculateDiscountRequest request)
        {
            if (request?.Items == null || !request.Items.Any())
                return BadRequest("Sepet boş.");

            var result = await _calculationService.CalculateAsync(request);
            return Ok(result);
        }
    }
}
