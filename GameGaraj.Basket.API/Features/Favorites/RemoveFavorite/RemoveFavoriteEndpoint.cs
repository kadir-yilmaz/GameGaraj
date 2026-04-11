using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Favorites.RemoveFavorite;

public static class RemoveFavoriteEndpoint
{
    public static RouteGroupBuilder MapRemoveFavoriteEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{productId}", async (string productId, ISender sender) =>
            {
                var result = await sender.Send(new RemoveFavoriteCommand(productId));
                return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Fail);
            })
            .WithName("RemoveFavorite")
            .MapToApiVersion(1, 0)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ServiceResult>(StatusCodes.Status400BadRequest);

        return group;
    }
}
