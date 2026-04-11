using System.Text.Json;
using GameGaraj.Basket.API.Data;
using GameGaraj.Basket.API.Dtos;
using Microsoft.Extensions.Caching.Distributed;

namespace GameGaraj.Basket.API.Services;

public class BasketService(IDistributedCache distributedCache, IIdentityService identityService)
{
    public async Task<Data.Basket?> GetBasketAsync(CancellationToken cancellationToken = default)
    {
        var basketString = await distributedCache.GetStringAsync(identityService.UserId, cancellationToken);
        
        if (string.IsNullOrEmpty(basketString))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Data.Basket>(basketString);
        }
        catch (JsonException)
        {
            // Data in Redis might be in old format (e.g. Id as int vs string)
            // Invalidating cache to start fresh.
            await distributedCache.RemoveAsync(identityService.UserId, cancellationToken);
            return null;
        }
    }

    public async Task SaveBasketAsync(Data.Basket basket, CancellationToken cancellationToken = default)
    {
        basket.UserId = identityService.UserId;
        var basketString = JsonSerializer.Serialize(basket);
        await distributedCache.SetStringAsync(identityService.UserId, basketString, cancellationToken);
    }

    public async Task DeleteBasketAsync(CancellationToken cancellationToken = default)
    {
        await distributedCache.RemoveAsync(identityService.UserId, cancellationToken);
    }
}
