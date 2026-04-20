using MediatR;
using GameGaraj.Order.Application.Dtos;

namespace GameGaraj.Order.Application.Commands
{
    /// <summary>
    /// Yeni sipariş oluşturma komutu
    /// </summary>
    public class CreateOrderCommand : IRequest<int>
    {
        public string BuyerId { get; set; } = string.Empty;
        public decimal OriginalTotalAmount { get; set; }
        public decimal CampaignDiscountAmount { get; set; }
        public decimal CouponDiscountAmount { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public string? CouponCode { get; set; }
        public string? AppliedCampaignName { get; set; }
        public List<OrderItemDto> OrderItems { get; set; } = new List<OrderItemDto>();
        public List<OrderPricingLedgerDto> OrderDiscounts { get; set; } = new List<OrderPricingLedgerDto>();
        public AddressDto Address { get; set; } = null!;
    }
}
