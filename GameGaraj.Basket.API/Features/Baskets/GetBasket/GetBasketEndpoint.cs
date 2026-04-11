using GameGaraj.Basket.API.Shared;
using MediatR;
using Asp.Versioning;

namespace GameGaraj.Basket.API.Features.Baskets.GetBasket;

public static class GetBasketEndpoint
{
    public static RouteGroupBuilder MapGetBasketEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ISender sender) =>
            {
                var result = await sender.Send(new GetBasketQuery());

                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.Fail);
            })
            .WithName("GetBasket")
            .MapToApiVersion(1, 0)
            .Produces<ServiceResult>(StatusCodes.Status200OK)
            .Produces<ServiceResult>(StatusCodes.Status400BadRequest);

        return group;
    }
}
