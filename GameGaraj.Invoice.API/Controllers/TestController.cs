using Microsoft.AspNetCore.Mvc;
using GameGaraj.Invoice.API.Services;
using GameGaraj.Invoice.API.Models;

namespace GameGaraj.Invoice.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly IEmailService _emailService;

        public TestController(IEmailService emailService)
        {
            _emailService = emailService;
        }

        [HttpPost("send-test-email")]
        public async Task<IActionResult> SendTestEmail([FromBody] TestEmailRequest request)
        {
            var invoiceData = new InvoiceData
            {
                OrderId = 999,
                CustomerName = "Test User",
                CustomerEmail = request.Email,
                OrderDate = DateTime.Now,
                TotalPrice = 100.00m,
                Items = new List<InvoiceItem>
                {
                    new InvoiceItem { ProductName = "Test Product", Price = 100.00m }
                }
            };

            var result = await _emailService.SendInvoiceEmailAsync(invoiceData);

            if (result)
            {
                return Ok(new { success = true, message = $"Test email sent to {request.Email}" });
            }
            else
            {
                return BadRequest(new { success = false, message = "Failed to send email" });
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return Ok(new { status = "healthy", timestamp = DateTime.Now });
        }
    }

    public class TestEmailRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}
