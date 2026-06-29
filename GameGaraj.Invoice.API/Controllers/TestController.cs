using Microsoft.AspNetCore.Mvc;
using GameGaraj.Invoice.API.Services;
using GameGaraj.Invoice.API.Models;
using MassTransit;
using GameGaraj.Shared.Events;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace GameGaraj.Invoice.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly IPdfGenerator _pdfGenerator;
        private readonly IStorageService _storageService;
        private readonly IPublishEndpoint _publishEndpoint;

        public TestController(
            IPdfGenerator pdfGenerator,
            IStorageService storageService,
            IPublishEndpoint publishEndpoint)
        {
            _pdfGenerator = pdfGenerator;
            _storageService = storageService;
            _publishEndpoint = publishEndpoint;
        }

        [HttpPost("generate-test-pdf")]
        public async Task<IActionResult> GenerateTestPdf()
        {
            var invoiceData = new InvoiceData
            {
                OrderId = 999,
                CustomerName = "Test User",
                CustomerEmail = "kadiryilmaz.dev@gmail.com",
                OrderDate = DateTime.Now,
                TotalPrice = 1500.50m,
                Items = new List<InvoiceItem>
                {
                    new InvoiceItem { ProductName = "GameGaraj Gaming PC", Price = 1200.00m },
                    new InvoiceItem { ProductName = "Mechanical Keyboard", Price = 300.50m }
                }
            };

            var pdfBytes = _pdfGenerator.GenerateInvoicePdf(invoiceData);
            var relativePath = await _storageService.UploadFileAsync(pdfBytes, "test-INV-000999.pdf", "application/pdf", default);

            return Ok(new { success = true, path = relativePath });
        }

        [HttpPost("send-test-notification")]
        public async Task<IActionResult> SendTestNotification([FromBody] TestEmailRequest request)
        {
            await _publishEndpoint.Publish(new SendNotification
            {
                Recipient = request.Email,
                Type = "Email",
                Title = "GameGaraj - Test Bildirimi",
                Body = "<h1>Merhaba!</h1><p>Bu Go Notification Service için gönderilmiş bir test bildirimdir.</p>",
                AttachmentPath = null,
                AttachmentName = null
            });

            return Ok(new { success = true, message = $"Test notification event published to {request.Email}" });
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
