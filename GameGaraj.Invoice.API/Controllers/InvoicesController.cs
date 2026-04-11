using Microsoft.AspNetCore.Mvc;

namespace GameGaraj.Invoice.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvoicesController : ControllerBase
    {
        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { Status = "Healthy", Service = "Invoice API" });
        }
    }
}
