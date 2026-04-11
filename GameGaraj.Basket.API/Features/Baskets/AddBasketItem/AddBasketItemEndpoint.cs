using GameGaraj.Basket.API.Shared;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace GameGaraj.Basket.API.Features.Baskets.AddBasketItem;

public static class AddBasketItemEndpoint
{
    public static RouteGroupBuilder MapAddBasketItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/items", async ([FromBody] AddBasketItemCommand command, ISender sender) =>
            {
                var result = await sender.Send(command);
                return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Fail);
            })
            .WithName("AddBasketItem")
            .MapToApiVersion(1, 0)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ServiceResult>(StatusCodes.Status400BadRequest);

        return group;
    }
}
