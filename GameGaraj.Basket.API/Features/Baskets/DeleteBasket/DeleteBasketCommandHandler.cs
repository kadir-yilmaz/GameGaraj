using GameGaraj.Basket.API.Services;
using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Baskets.DeleteBasket;

public class DeleteBasketCommandHandler(BasketService basketService)
    : IRequestHandler<DeleteBasketCommand, ServiceResult>
{
    public async Task<ServiceResult> Handle(DeleteBasketCommand request, CancellationToken cancellationToken)
    {
        await basketService.DeleteBasketAsync(cancellationToken);
        return ServiceResult.SuccessAsNoContent();
    }
}
