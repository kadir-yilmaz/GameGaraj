using GameGaraj.Catalog.API.Repositories.Abstract;
using GameGaraj.Shared.Events;
using MassTransit;
using MongoDB.Bson;

namespace GameGaraj.Catalog.API.Consumers
{
    public class PaymentFailedConsumer : IConsumer<PaymentFailed>
    {
        private readonly IProductRepository _productRepository;
        private readonly ILogger<PaymentFailedConsumer> _logger;

        public PaymentFailedConsumer(
            IProductRepository productRepository,
            ILogger<PaymentFailedConsumer> logger)
        {
            _productRepository = productRepository;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PaymentFailed> context)
        {
            _logger.LogWarning($"[PaymentFailedConsumer] Payment failed for OrderId: {context.Message.OrderId}. Releasing reserved stock.");

            foreach (var item in context.Message.OrderItems)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product != null)
                {
                    // Ödeme başarısız: Rezervasyonu iade et (tekrar satılabilir yap)
                    product.ReservedStock -= item.Quantity;
                    await _productRepository.UpdateAsync(product);
                    _logger.LogInformation($"[PaymentFailedConsumer] Stock released for {product.Name}. Available: {product.AvailableStock}");
                }
            }

            await Task.CompletedTask;
        }
    }
}
