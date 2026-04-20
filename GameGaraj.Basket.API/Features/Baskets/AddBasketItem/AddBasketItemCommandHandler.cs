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
        
        var existingItem = basket.Items.FirstOrDefault(x => x.Id == request.Id);
        var newItem = new BasketItem
        {
            Id = request.Id,
            Name = request.Name,
            CategoryId = request.CategoryId ?? string.Empty,
            Price = request.Price, 
            PictureUrl = request.PictureUrl,
            Quantity = request.Quantity
        };

        if (existingItem != null)
        {
            var index = basket.Items.IndexOf(existingItem);
            basket.Items[index] = newItem;
        }
        else
        {
            basket.Items.Add(newItem);
        }

        await basketService.SaveBasketAsync(basket, cancellationToken);

        return ServiceResult.SuccessAsNoContent();
    }
}
