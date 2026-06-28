using MassTransit;
using GameGaraj.Invoice.API.Models;
using GameGaraj.Invoice.API.Services;
using GameGaraj.Shared.Events;

using GameGaraj.Shared.Observability.Metrics;

namespace GameGaraj.Invoice.API.Consumers
{
    /// <summary>
    /// InvoiceRequested event'ini dinler ve fatura emaili gönderir.
    /// </summary>
    public class InvoiceRequestedConsumer : IConsumer<InvoiceRequested>
    {
        private readonly IEmailService _emailService;
        private readonly InvoiceMetrics _metrics;

        public InvoiceRequestedConsumer(
            IEmailService emailService,
            InvoiceMetrics metrics)
        {
            _emailService = emailService;
            _metrics = metrics;
        }

        public async Task Consume(ConsumeContext<InvoiceRequested> context)
        {
            Console.WriteLine($"[InvoiceRequestedConsumer] Received InvoiceRequested for OrderId: {context.Message.OrderId}");
            Console.WriteLine($"[InvoiceRequestedConsumer] Sending to: {context.Message.CustomerEmail}");

            _metrics.InvoiceGenerated();

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

            bool result;
            using (var tracker = _metrics.TrackGeneration())
            {
                result = await _emailService.SendInvoiceEmailAsync(invoiceData);
            }
            
            if (result)
            {
                Console.WriteLine($"[InvoiceRequestedConsumer] ✅ Invoice email sent successfully");
                _metrics.EmailSent();
            }
            else
            {
                Console.WriteLine($"[InvoiceRequestedConsumer] ❌ Failed to send invoice email");
                _metrics.EmailFailed();
            }
        }
    }
}
