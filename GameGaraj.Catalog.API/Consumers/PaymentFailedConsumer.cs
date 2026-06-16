using GameGaraj.Catalog.API.Data;
using GameGaraj.Shared.Events;
using MassTransit;

namespace GameGaraj.Catalog.API.Consumers
{
    public class PaymentFailedConsumer : IConsumer<PaymentFailed>
    {
        private readonly CatalogDbContext _context;
        private readonly ILogger<PaymentFailedConsumer> _logger;

        public PaymentFailedConsumer(
            CatalogDbContext context,
            ILogger<PaymentFailedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PaymentFailed> context)
        {
            _logger.LogWarning($"[PaymentFailedConsumer] Payment failed for OrderId: {context.Message.OrderId}. Releasing reserved stock.");

            foreach (var item in context.Message.OrderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    // Ödeme başarısız: Rezervasyonu iade et (tekrar satılabilir yap)
                    product.ReservedStock -= item.Quantity;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"[PaymentFailedConsumer] Stock released for {product.Name}. Available: {product.AvailableStock}");
                }
            }

            await Task.CompletedTask;
        }
    }
}
