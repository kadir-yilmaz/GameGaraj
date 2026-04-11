using System.Net;
using GameGaraj.Basket.API.Services;
using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Baskets.DeleteBasketItem;

public class DeleteBasketItemCommandHandler(BasketService basketService)
    : IRequestHandler<DeleteBasketItemCommand, ServiceResult>
{
    public async Task<ServiceResult> Handle(DeleteBasketItemCommand request, CancellationToken cancellationToken)
    {
        var basket = await basketService.GetBasketAsync(cancellationToken);

        if (basket is null)
        {
            return ServiceResult.Error("Basket not found", HttpStatusCode.NotFound);
        }

        var item = basket.Items.FirstOrDefault(x => x.Id == request.ProductId);
        if (item is null)
        {
            return ServiceResult.Error("Item not found in basket", HttpStatusCode.NotFound);
        }

        basket.Items.Remove(item);

        await basketService.SaveBasketAsync(basket, cancellationToken);

        return ServiceResult.SuccessAsNoContent();
    }
}
