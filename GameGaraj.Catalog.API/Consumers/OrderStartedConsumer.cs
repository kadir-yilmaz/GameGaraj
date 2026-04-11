using GameGaraj.Catalog.API.Repositories.Abstract;
using GameGaraj.Shared.Events;
using MassTransit;
using MongoDB.Bson;

namespace GameGaraj.Catalog.API.Consumers
{
    public class OrderStartedConsumer : IConsumer<OrderStarted>
    {
        private readonly IProductRepository _productRepository;
        private readonly ILogger<OrderStartedConsumer> _logger;
        private readonly IPublishEndpoint _publishEndpoint;

        public OrderStartedConsumer(
            IProductRepository productRepository,
            ILogger<OrderStartedConsumer> logger,
            IPublishEndpoint publishEndpoint)
        {
            _productRepository = productRepository;
            _logger = logger;
            _publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<OrderStarted> context)
        {
            _logger.LogInformation($"[OrderStartedConsumer] Processing order: {context.Message.OrderId}");

            bool allReserved = true;
            string failReason = "";

            foreach (var item in context.Message.OrderItems)
            {
                var product = await _productRepository.GetByIdAsync(item.ProductId);
                if (product == null)
                {
                    allReserved = false;
                    failReason = $"Ürün bulunamadı: {item.ProductId}";
                    break;
                }

                if (product.AvailableStock < item.Quantity)
                {
                    allReserved = false;
                    failReason = $"Yetersiz stok: {product.Name} (Mevcut: {product.AvailableStock}, İstenen: {item.Quantity})";
                    break;
                }

                // Stok rezerve et
                product.ReservedStock += item.Quantity;
                await _productRepository.UpdateAsync(product);
                _logger.LogInformation($"[OrderStartedConsumer] Reserved {item.Quantity} for {product.Name}");
            }

            if (allReserved)
            {
                _logger.LogInformation($"[OrderStartedConsumer] All items reserved for OrderId: {context.Message.OrderId}");
                await _publishEndpoint.Publish(new StockReserved { OrderId = context.Message.OrderId });
            }
            else
            {
                _logger.LogWarning($"[OrderStartedConsumer] Reservation failed for OrderId: {context.Message.OrderId}. Reason: {failReason}");

                // Geri alma (Compensating Transaction) - bu basitleştirilmiş bir örnek. 
                // Gerçek senaryoda daha önce rezerve edilenleri geri bırakmak gerekir.
                // Şimdilik basitleştirilmiş tutuyoruz.

                await _publishEndpoint.Publish(new StockNotReserved
                {
                    OrderId = context.Message.OrderId,
                    Reason = failReason
                });
            }
        }
    }
}
