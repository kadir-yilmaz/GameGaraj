using MassTransit;
using GameGaraj.Invoice.API.Models;
using GameGaraj.Invoice.API.Services;
using GameGaraj.Shared.Events;

namespace GameGaraj.Invoice.API.Consumers
{
    /// <summary>
    /// InvoiceRequested event'ini dinler ve fatura emaili gönderir.
    /// </summary>
    public class InvoiceRequestedConsumer : IConsumer<InvoiceRequested>
    {
        private readonly IEmailService _emailService;

        public InvoiceRequestedConsumer(IEmailService emailService)
        {
            _emailService = emailService;
        }

        public async Task Consume(ConsumeContext<InvoiceRequested> context)
        {
            Console.WriteLine($"[InvoiceRequestedConsumer] Received InvoiceRequested for OrderId: {context.Message.OrderId}");
            Console.WriteLine($"[InvoiceRequestedConsumer] Sending to: {context.Message.CustomerEmail}");

            var invoiceData = new InvoiceData
            {
                OrderId = context.Message.OrderId,
                CustomerName = context.Message.CustomerName,
                CustomerEmail = context.Message.CustomerEmail,
                OrderDate = context.Message.OrderDate,
                TotalPrice = context.Message.TotalPrice,
                Items = context.Message.Items.Select(x => new InvoiceItem
                {
                    ProductName = x.ProductName,
                    Price = x.Price
                }).ToList()
            };

            var result = await _emailService.SendInvoiceEmailAsync(invoiceData);
            
            if (result)
            {
                Console.WriteLine($"[InvoiceRequestedConsumer] ✅ Invoice email sent successfully");
            }
            else
            {
                Console.WriteLine($"[InvoiceRequestedConsumer] ❌ Failed to send invoice email");
            }
        }
    }
}
