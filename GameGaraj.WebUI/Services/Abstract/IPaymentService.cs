using GameGaraj.WebUI.Models.Orders;

namespace GameGaraj.WebUI.Services.Abstract
{
    public interface IPaymentService
    {
        Task<PaymentResult> ProcessPayment(PaymentRequest paymentRequest);
    }
}
