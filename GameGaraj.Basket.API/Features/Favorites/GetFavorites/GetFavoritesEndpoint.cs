using GameGaraj.Basket.API.Shared;
using MediatR;

namespace GameGaraj.Basket.API.Features.Favorites.GetFavorites;

public static class GetFavoritesEndpoint
{
    public static RouteGroupBuilder MapGetFavoritesEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ISender sender) =>
            {
                var result = await sender.Send(new GetFavoritesQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result.Fail);
            })
            .WithName("GetFavorites")
            .MapToApiVersion(1, 0)
            .Produces<GetFavoritesResponse>(StatusCodes.Status200OK)
            .Produces<ServiceResult>(StatusCodes.Status400BadRequest);

        return group;
    }
}
