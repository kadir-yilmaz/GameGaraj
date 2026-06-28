using GameGaraj.Campaign.API.Models;
using GameGaraj.Campaign.API.Services.Abstract;
using Microsoft.AspNetCore.Mvc;

using GameGaraj.Shared.Observability.Metrics;

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
        private readonly CampaignMetrics _metrics;

        public CampaignCalculateController(
            ICampaignCalculationService calculationService,
            CampaignMetrics metrics)
        {
            _calculationService = calculationService;
            _metrics = metrics;
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
            _metrics.CampaignCalculated();
            return Ok(result);
        }
    }
}
