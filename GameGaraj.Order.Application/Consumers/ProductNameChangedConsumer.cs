using MassTransit;
using Microsoft.EntityFrameworkCore;
using GameGaraj.Order.Infrastructure;
using GameGaraj.Shared.Events;

namespace GameGaraj.Order.Application.Consumers
{
    /// <summary>
    /// ProductNameChanged event'ini dinler ve ilgili OrderItem'ların ProductName'ini günceller.
    /// Eventual Consistency: Catalog'da ürün adı değiştiğinde Order DB'deki sipariş kalemleri de güncellenir.
    /// </summary>
    public class ProductNameChangedConsumer : IConsumer<ProductNameChanged>
    {
        private readonly OrderDbContext _context;

        public ProductNameChangedConsumer(OrderDbContext context)
        {
            _context = context;
        }

        public async Task Consume(ConsumeContext<ProductNameChanged> context)
        {
            Console.WriteLine($"[ProductNameChangedConsumer] Received for ProductId: {context.Message.ProductId}, NewName: {context.Message.NewName}");

            var orderItems = await _context.OrderItems
                .Where(x => x.ProductId == context.Message.ProductId)
                .ToListAsync();

            if (!orderItems.Any())
            {
                Console.WriteLine($"[ProductNameChangedConsumer] No order items found for ProductId: {context.Message.ProductId}");
                return;
            }

            foreach (var item in orderItems)
            {
                item.ProductName = context.Message.NewName;
            }

            await _context.SaveChangesAsync();

            Console.WriteLine($"[ProductNameChangedConsumer] ✅ Updated {orderItems.Count} order items with new product name");
        }
    }
}
