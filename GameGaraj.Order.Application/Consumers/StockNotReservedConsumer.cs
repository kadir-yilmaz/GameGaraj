using GameGaraj.Order.Infrastructure;
using GameGaraj.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GameGaraj.Order.Domain.Enums;

namespace GameGaraj.Order.Application.Consumers
{
    public class StockNotReservedConsumer : IConsumer<StockNotReserved>
    {
        private readonly OrderDbContext _context;
        private readonly ILogger<StockNotReservedConsumer> _logger;

        public StockNotReservedConsumer(OrderDbContext context, ILogger<StockNotReservedConsumer> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<StockNotReserved> context)
        {
            _logger.LogWarning($"[StockNotReservedConsumer] Stock reservation failed for OrderId: {context.Message.OrderId}. Reason: {context.Message.Reason}");

            var order = await _context.Orders.FirstOrDefaultAsync(x => x.Id == context.Message.OrderId);

            if (order != null)
            {
                order.Status = OrderStatus.Failed;
                await _context.SaveChangesAsync();
                _logger.LogInformation($"[StockNotReservedConsumer] Order {order.Id} status updated to Failed due to insufficient stock.");
            }
        }
    }
}
