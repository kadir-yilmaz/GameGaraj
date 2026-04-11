using GameGaraj.Basket.API.Data;
using GameGaraj.Basket.API.Services;
using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Baskets.AddBasketItem;

public class AddBasketItemCommandHandler(BasketService basketService, IIdentityService identityService)
    : IRequestHandler<AddBasketItemCommand, ServiceResult>
{
    public async Task<ServiceResult> Handle(AddBasketItemCommand request, CancellationToken cancellationToken)
    {
        var basket = await basketService.GetBasketAsync(cancellationToken);

        if (basket is null)
        {
            basket = new Data.Basket { UserId = identityService.UserId };
        }
        
        // Remove existing item if exists to update it (simple implementation)
        var existingItem = basket.Items.FirstOrDefault(x => x.Id == request.Id);
        if (existingItem != null)
        {
            basket.Items.Remove(existingItem);
        }

        basket.Items.Add(new BasketItem
        {
            Id = request.Id,
            Name = request.Name,
            Price = request.Price, 
            PictureUrl = request.PictureUrl,
            Quantity = request.Quantity
        });

        await basketService.SaveBasketAsync(basket, cancellationToken);

        return ServiceResult.SuccessAsNoContent();
    }
}
