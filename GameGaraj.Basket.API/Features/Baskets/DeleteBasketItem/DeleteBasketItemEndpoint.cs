using GameGaraj.Basket.API.Shared;
using MediatR;
using Asp.Versioning;

namespace GameGaraj.Basket.API.Features.Baskets.DeleteBasketItem;

public static class DeleteBasketItemEndpoint
{
    public static RouteGroupBuilder MapDeleteBasketItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/items/{productId}", async (string productId, ISender sender) =>
            {
                var result = await sender.Send(new DeleteBasketItemCommand(productId));
                return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Fail);
            })
            .WithName("DeleteBasketItem")
            .MapToApiVersion(1, 0)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ServiceResult>(StatusCodes.Status400BadRequest);

        return group;
    }
}
