using MediatR;
using GameGaraj.Order.Application.Dtos;

namespace GameGaraj.Order.Application.Commands
{
    /// <summary>
    /// Yeni sipariş oluşturma komutu
    /// </summary>
    public class CreateOrderCommand : IRequest<CreatedOrderDto>
    {
        public string BuyerId { get; set; } = string.Empty;
        public List<OrderItemDto> OrderItems { get; set; } = new List<OrderItemDto>();
        public AddressDto Address { get; set; } = null!;
    }
}
