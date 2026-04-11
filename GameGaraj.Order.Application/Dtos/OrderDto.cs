using GameGaraj.Order.Domain.Enums;

namespace GameGaraj.Order.Application.Dtos
{
    public class OrderDto
    {
        public int Id { get; set; }
        public DateTime CreatedDate { get; set; }
        public AddressDto Address { get; set; } = null!;
        public string BuyerId { get; set; } = string.Empty;
        public OrderStatus Status { get; set; }
        public List<OrderItemDto> OrderItems { get; set; } = new List<OrderItemDto>();
    }
}
