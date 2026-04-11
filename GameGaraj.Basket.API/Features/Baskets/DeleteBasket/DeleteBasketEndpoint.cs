using GameGaraj.Basket.API.Shared;
using MediatR;
using Asp.Versioning;

namespace GameGaraj.Basket.API.Features.Baskets.DeleteBasket;

public static class DeleteBasketEndpoint
{
    public static RouteGroupBuilder MapDeleteBasketEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/", async (ISender sender) =>
            {
                var result = await sender.Send(new DeleteBasketCommand());
                return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Fail);
            })
            .WithName("DeleteBasket")
            .MapToApiVersion(1, 0)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ServiceResult>(StatusCodes.Status400BadRequest);

        return group;
    }
}
