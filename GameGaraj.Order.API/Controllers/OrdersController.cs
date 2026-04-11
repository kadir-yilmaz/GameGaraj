using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GameGaraj.Order.Application.Commands;
using GameGaraj.Order.Application.Dtos;
using GameGaraj.Order.Application.Mapping;
using GameGaraj.Order.Domain.Entities;
using GameGaraj.Order.Domain.Enums;
using GameGaraj.Order.Infrastructure;

namespace GameGaraj.Order.API.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly Infrastructure.OrderDbContext _context;
        private readonly MediatR.IMediator _mediator;

        public OrdersController(Infrastructure.OrderDbContext context, MediatR.IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        /// <summary>
        /// Kullanıcıya ait siparişleri getirir
        /// </summary>
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetOrders(string userId)
        {
            Console.WriteLine($"[OrdersController] GetOrders called for userId: {userId}");

            var orders = await _context.Orders
                .Include(x => x.OrderItems)
                .Include(x => x.DeliveryAddress)
                .Include(x => x.InvoiceAddress)
                .Where(x => x.BuyerId == userId)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            Console.WriteLine($"[OrdersController] Found {orders.Count} orders for userId: {userId}");

            if (orders.Any())
            {
                foreach (var order in orders)
                {
                    Console.WriteLine($"  - Order #{order.Id}, Status: {order.Status}, Items: {order.OrderItems.Count}, Date: {order.CreatedDate}");
                }
            }

            var orderDtos = ObjectMapper.Mapper.Map<List<OrderDto>>(orders);
            return Ok(orderDtos);
        }

        /// <summary>
        /// Tüm siparişleri getirir (Admin için)
        /// </summary>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllOrders()
        {
            Console.WriteLine($"[OrdersController] GetAllOrders called");

            var orders = await _context.Orders
                .Include(x => x.OrderItems)
                .Include(x => x.DeliveryAddress)
                .Include(x => x.InvoiceAddress)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            Console.WriteLine($"[OrdersController] Found {orders.Count} total orders");

            var orderDtos = ObjectMapper.Mapper.Map<List<OrderDto>>(orders);
            return Ok(orderDtos);
        }

        /// <summary>
        /// Sipariş durumunu günceller
        /// </summary>
        [HttpPut("{orderId}/status/{status}")]
        public async Task<IActionResult> UpdateOrderStatus(int orderId, int status)
        {
            Console.WriteLine($"[OrdersController] UpdateOrderStatus called - OrderId: {orderId}, Status: {status}");

            var order = await _context.Orders.FindAsync(orderId);

            if (order == null)
            {
                Console.WriteLine($"[OrdersController] Order not found: {orderId}");
                return NotFound();
            }

            order.Status = (OrderStatus)status;
            await _context.SaveChangesAsync();

            Console.WriteLine($"[OrdersController] Order {orderId} status updated to {status}");

            return Ok();
        }

        /// <summary>
        /// Kullanıcının satın aldığı tüm ürün ID'lerini döner
        /// </summary>
        [HttpGet("{userId}/owned-products")]
        public async Task<IActionResult> GetOwnedProductIds(string userId)
        {
            var ownedProductIds = await _context.Orders
                .Where(x => x.BuyerId == userId && x.Status == OrderStatus.Completed)
                .SelectMany(x => x.OrderItems)
                .Select(x => x.ProductId)
                .Distinct()
                .ToListAsync();

            return Ok(ownedProductIds);
        }

        /// <summary>
        /// Kullanıcının belirli bir ürüne sahip olup olmadığını kontrol eder
        /// </summary>
        [HttpGet("{userId}/owns/{productId}")]
        public async Task<IActionResult> CheckProductOwnership(string userId, string productId)
        {
            var owns = await _context.Orders
                .Where(x => x.BuyerId == userId && x.Status == OrderStatus.Completed)
                .SelectMany(x => x.OrderItems)
                .AnyAsync(x => x.ProductId == productId);

            if (owns)
            {
                var purchaseDate = await _context.Orders
                    .Where(x => x.BuyerId == userId && x.Status == OrderStatus.Completed)
                    .Where(x => x.OrderItems.Any(oi => oi.ProductId == productId))
                    .Select(x => x.CreatedDate)
                    .FirstOrDefaultAsync();

                return Ok(new { Owns = true, PurchaseDate = purchaseDate });
            }

            return Ok(new { Owns = false, PurchaseDate = (DateTime?)null });
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrder(CreateOrderCommand command)
        {
            Console.WriteLine($"[OrdersController] POST CreateOrder called via Mediator. BuyerId: {command.BuyerId}");
            var result = await _mediator.Send(command);
            return Ok(result);
        }
    }
}
