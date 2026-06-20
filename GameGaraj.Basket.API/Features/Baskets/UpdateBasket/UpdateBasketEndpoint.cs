using GameGaraj.Basket.API.Shared;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace GameGaraj.Basket.API.Features.Baskets.UpdateBasket;

public static class UpdateBasketEndpoint
{
    public static RouteGroupBuilder MapUpdateBasketEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] UpdateBasketCommand command, ISender sender) =>
            {
                var result = await sender.Send(command);

                return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Fail);
            })
            .WithName("UpdateBasket")
            .MapToApiVersion(1, 0)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ServiceResult>(StatusCodes.Status400BadRequest);

        return group;
    }
}
