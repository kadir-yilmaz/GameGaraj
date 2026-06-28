using GameGaraj.Basket.API.Services;
using GameGaraj.Basket.API.Shared;
using GameGaraj.Shared.Observability.Metrics;
using MediatR;

namespace GameGaraj.Basket.API.Features.Baskets.DeleteBasket;

public class DeleteBasketCommandHandler(BasketService basketService, BasketMetrics basketMetrics)
    : IRequestHandler<DeleteBasketCommand, ServiceResult>
{
    public async Task<ServiceResult> Handle(DeleteBasketCommand request, CancellationToken cancellationToken)
    {
        await basketService.DeleteBasketAsync(cancellationToken);
        
        basketMetrics.BasketDeleted();

        return ServiceResult.SuccessAsNoContent();
    }
}
