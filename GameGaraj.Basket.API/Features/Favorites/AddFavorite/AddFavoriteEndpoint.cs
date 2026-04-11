using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Favorites.AddFavorite;

public static class AddFavoriteEndpoint
{
    public static RouteGroupBuilder MapAddFavoriteEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{productId}", async (string productId, ISender sender) =>
            {
                var result = await sender.Send(new AddFavoriteCommand(productId));
                return result.IsSuccess ? Results.NoContent() : Results.BadRequest(result.Fail);
            })
            .WithName("AddFavorite")
            .MapToApiVersion(1, 0)
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ServiceResult>(StatusCodes.Status400BadRequest);

        return group;
    }
}
