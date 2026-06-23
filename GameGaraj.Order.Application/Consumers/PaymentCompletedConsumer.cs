using MassTransit;
using Microsoft.EntityFrameworkCore;
using GameGaraj.Order.Domain.Enums;
using GameGaraj.Order.Infrastructure;
using GameGaraj.Shared.Events;

namespace GameGaraj.Order.Application.Consumers
{
    /// <summary>
    /// PaymentCompleted event'ini dinler ve sipariş durumunu Completed olarak günceller.
    /// </summary>
    public class PaymentCompletedConsumer : IConsumer<PaymentCompleted>
    {
        private readonly OrderDbContext _context;
        private readonly IPublishEndpoint _publishEndpoint;

        public PaymentCompletedConsumer(OrderDbContext context, IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
        }

        public async Task Consume(ConsumeContext<PaymentCompleted> context)
        {
            Console.WriteLine($"[PaymentCompletedConsumer] Received PaymentCompleted for OrderId: {context.Message.OrderId}");

            var order = await _context.Orders.FirstOrDefaultAsync(x => x.Id == context.Message.OrderId);
            
            if (order == null)
            {
                Console.WriteLine($"[PaymentCompletedConsumer] ❌ Order not found: {context.Message.OrderId}");
                return;
            }

            // Ödeme başarılı, sipariş hazırlanmaya başlasın
            order.Status = (int)OrderStatus.Completed; // Önce Completed yap (ödeme onayı)
            await _context.SaveChangesAsync();

            Console.WriteLine($"[PaymentCompletedConsumer] ✅ Order {order.Id} status updated to Completed (Payment confirmed)");

            // 📤 CouponRewardTriggered event publish et
            try
            {
                await _publishEndpoint.Publish(new CouponRewardTriggered
                {
                    OrderId = order.Id,
                    UserId = order.BuyerId,
                    Amount = order.TotalPaidAmount,
                    PurchaseDate = order.CreatedDate
                });
                Console.WriteLine($"[PaymentCompletedConsumer] 📤 CouponRewardTriggered event published for OrderId: {order.Id}, User: {order.BuyerId}, Amount: {order.TotalPaidAmount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PaymentCompletedConsumer] ❌ Failed to publish CouponRewardTriggered event: {ex.Message}");
            }
        }
    }
}
