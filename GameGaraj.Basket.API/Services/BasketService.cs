using System.Text.Json;
using GameGaraj.Basket.API.Data;
using GameGaraj.Basket.API.Dtos;
using Microsoft.Extensions.Caching.Distributed;

namespace GameGaraj.Basket.API.Services;

public class BasketService(IDistributedCache distributedCache, IIdentityService identityService)
{
    public async Task<Data.Basket?> GetBasketAsync(CancellationToken cancellationToken = default)
    {
        var userId = identityService.UserId;
        var basketString = await distributedCache.GetStringAsync(userId, cancellationToken);
        
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
            await distributedCache.RemoveAsync(userId, cancellationToken);
            return null;
        }
    }

    public async Task SaveBasketAsync(Data.Basket basket, CancellationToken cancellationToken = default)
    {
        var userId = identityService.UserId;
        basket.UserId = userId;
        var basketString = JsonSerializer.Serialize(basket);

        var expiration = identityService.IsGuest ? TimeSpan.FromDays(1) : TimeSpan.FromDays(30);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        };
        await distributedCache.SetStringAsync(userId, basketString, options, cancellationToken);
    }


    public async Task DeleteBasketAsync(CancellationToken cancellationToken = default)
    {
        await distributedCache.RemoveAsync(identityService.UserId, cancellationToken);
    }
}
