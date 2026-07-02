using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GameGaraj.Order.Application.Commands;
using GameGaraj.Order.Application.Dtos;
using GameGaraj.Order.Application.Mapping;
using GameGaraj.Order.Domain.Entities;
using GameGaraj.Order.Domain.Enums;
using GameGaraj.Order.Infrastructure;
using GameGaraj.Shared.Observability;

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
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.OrderItems)
                .Include(x => x.OrderPricingLedgers)
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
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.OrderItems)
                .Include(x => x.OrderPricingLedgers)
                .Include(x => x.DeliveryAddress)
                .Include(x => x.InvoiceAddress)
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            Console.WriteLine($"[OrdersController] Found {orders.Count} total orders");

            var orderDtos = ObjectMapper.Mapper.Map<List<OrderDto>>(orders);
            return Ok(orderDtos);
        }

        [HttpGet("admin")]
        public async Task<IActionResult> GetAdminOrders(
            [FromQuery] string? q = null,
            [FromQuery] int? status = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 6, 60);

            var ordersQuery = _context.Orders
                .AsNoTracking()
                .AsSplitQuery()
                .Include(x => x.OrderItems)
                .Include(x => x.OrderPricingLedgers)
                .Include(x => x.DeliveryAddress)
                .Include(x => x.InvoiceAddress)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var normalizedQuery = q.Trim().ToLower();
                var pattern = $"%{normalizedQuery}%";
                ordersQuery = ordersQuery.Where(order =>
                    EF.Functions.Like(order.Id.ToString(), pattern) ||
                    EF.Functions.Like((order.BuyerId ?? string.Empty).ToLower(), pattern) ||
                    EF.Functions.Like(((order.DeliveryAddress.FirstName ?? string.Empty) + " " + (order.DeliveryAddress.LastName ?? string.Empty)).ToLower(), pattern) ||
                    EF.Functions.Like((order.DeliveryAddress.Email ?? string.Empty).ToLower(), pattern) ||
                    EF.Functions.Like((order.DeliveryAddress.PhoneNumber ?? string.Empty).ToLower(), pattern));
            }

            if (status.HasValue)
            {
                ordersQuery = ordersQuery.Where(order => order.Status == status.Value);
            }

            if (dateFrom.HasValue)
            {
                var from = dateFrom.Value.Date;
                ordersQuery = ordersQuery.Where(order => order.CreatedDate >= from);
            }

            if (dateTo.HasValue)
            {
                var toExclusive = dateTo.Value.Date.AddDays(1);
                ordersQuery = ordersQuery.Where(order => order.CreatedDate < toExclusive);
            }

            var totalCount = await ordersQuery.LongCountAsync();
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

            var orders = await ordersQuery
                .OrderByDescending(order => order.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var orderDtos = ObjectMapper.Mapper.Map<List<OrderDto>>(orders);

            return Ok(new PagedResultDto<OrderDto>
            {
                Items = orderDtos,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
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

            order.Status = status;
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
            var ownershipStatuses = new[]
            {
                (int)OrderStatus.Delivered
            };

            var ownedProductIds = await _context.Orders
                .Where(x => x.BuyerId == userId && ownershipStatuses.Contains(x.Status))
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
            var ownershipStatuses = new[]
            {
                (int)OrderStatus.Delivered
            };

            var owns = await _context.Orders
                .Where(x => x.BuyerId == userId && ownershipStatuses.Contains(x.Status))
                .SelectMany(x => x.OrderItems)
                .AnyAsync(x => x.ProductId == productId);

            if (owns)
            {
                var purchaseDate = await _context.Orders
                    .Where(x => x.BuyerId == userId && ownershipStatuses.Contains(x.Status))
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
            using var activity = AppDiagnostics.StartActivity("Order API Create Order");
            activity?.SetTag("user.id", command.BuyerId);
            activity?.SetTag("order.items.count", command.OrderItems?.Count ?? 0);
            activity?.SetTag("order.total_paid", command.TotalPaidAmount);

            Console.WriteLine($"[OrdersController] POST CreateOrder called via Mediator. BuyerId: {command.BuyerId}");
            var result = await _mediator.Send(command);

            activity?.SetTag("order.id", result);
            activity?.SetTag("saga.step", "OrderCreated");

            return Ok(result);
        }
    }
}
