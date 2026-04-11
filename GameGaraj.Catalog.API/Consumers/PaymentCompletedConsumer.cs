using GameGaraj.Catalog.API.Repositories.Abstract;
using GameGaraj.Shared.Events;
using MassTransit;
using MongoDB.Bson;

namespace GameGaraj.Catalog.API.Consumers
{
    public class PaymentCompletedConsumer : IConsumer<PaymentCompleted>
    {
        private readonly IProductRepository _productRepository;
        private readonly ILogger<PaymentCompletedConsumer> _logger;

        public PaymentCompletedConsumer(
            IProductRepository productRepository,
            ILogger<PaymentCompletedConsumer> logger)
        {
            _productRepository = productRepository;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PaymentCompleted> context)
        {
            _logger.LogInformation($"[PaymentCompletedConsumer] Payment successful for OrderId: {context.Message.OrderId}. Finalizing stock.");

            foreach (var item in context.Message.OrderItems)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product != null)
                {
                    _logger.LogInformation($"[PaymentCompletedConsumer] Processing stock for {product.Name}. Current Total: {product.TotalStock}, Current Reserved: {product.ReservedStock}");

                    // Ödeme başarılı: Fiziksel stoğu her durumda düş (failsafe)
                    product.TotalStock -= item.Quantity;

                    // Rezervasyonu sadece varsa ve miktar yeterliyse düş
                    if (product.ReservedStock >= item.Quantity)
                    {
                        product.ReservedStock -= item.Quantity;
                        _logger.LogInformation($"[PaymentCompletedConsumer] Deducted from ReservedStock for {product.Name}");
                    }
                    else if (product.ReservedStock > 0)
                    {
                        _logger.LogWarning($"[PaymentCompletedConsumer] Partial ReservedStock ({product.ReservedStock}) for {product.Name}. Clearing it.");
                        product.ReservedStock = 0;
                    }

                    await _productRepository.UpdateAsync(product);
                    _logger.LogInformation($"[PaymentCompletedConsumer] Stock finalized for {product.Name}. New Total: {product.TotalStock}, New Reserved: {product.ReservedStock}");
                }
            }

            await Task.CompletedTask;
        }
    }
}
