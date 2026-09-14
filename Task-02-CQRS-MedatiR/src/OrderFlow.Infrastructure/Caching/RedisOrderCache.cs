using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions;

namespace OrderFlow.Infrastructure.Caching;

public class RedisOrderCache(
    IDistributedCache cache,
    IConfiguration configuration,
    ILogger<RedisOrderCache> logger) : IOrderCache
{
    private static string Key(Guid orderId) => $"order:{orderId}";

    public async Task<string?> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await cache.GetStringAsync(Key(orderId), cancellationToken);
            logger.LogInformation("Cache {Result} for {Key}", json is null ? "MISS" : "HIT", Key(orderId));
            return json;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis unavailable on GET for {Key} — falling back to SQL Server", Key(orderId));
            return null;
        }
    }

    public async Task SetAsync(Guid orderId, string json, CancellationToken cancellationToken = default)
    {
        var seconds = int.TryParse(configuration["Cache:OrderDetailsDurationSeconds"], out var parsed) && parsed > 0
            ? parsed
            : 300;

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(seconds)
        };

        try
        {
            await cache.SetStringAsync(Key(orderId), json, options, cancellationToken);
            logger.LogInformation("Cached {Key} for {Seconds}s", Key(orderId), seconds);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis unavailable on SET for {Key} — skipping cache write", Key(orderId));
        }
    }

    public async Task RemoveAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.RemoveAsync(Key(orderId), cancellationToken);
            logger.LogInformation("Invalidated cache {Key}", Key(orderId));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis unavailable on REMOVE for {Key} — skipping invalidation", Key(orderId));
        }
    }
}
