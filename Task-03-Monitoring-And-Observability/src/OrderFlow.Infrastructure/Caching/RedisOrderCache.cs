using System.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Observability;

namespace OrderFlow.Infrastructure.Caching;

public class RedisOrderCache(
    IDistributedCache cache,
    IConfiguration configuration,
    ILogger<RedisOrderCache> logger) : IOrderCache
{
    private static string Key(Guid orderId) => $"order:{orderId}";

    public async Task<string?> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        using var activity = OrderFlowDiagnostics.ActivitySource.StartActivity("cache get");
        activity?.SetTag("cache.key", Key(orderId));

        try
        {
            var json = await cache.GetStringAsync(Key(orderId), cancellationToken);

            activity?.SetTag("cache.hit", json is not null);

            if (json is null)
                logger.LogInformation("Cache MISS for order {OrderId}", orderId);
            else
                logger.LogInformation("Cache HIT for order {OrderId}", orderId);

            return json;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogWarning(ex, "Redis unavailable on GET for order {OrderId} — falling back to SQL Server", orderId);
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

        using var activity = OrderFlowDiagnostics.ActivitySource.StartActivity("cache set");
        activity?.SetTag("cache.key", Key(orderId));

        try
        {
            await cache.SetStringAsync(Key(orderId), json, options, cancellationToken);
            logger.LogInformation("Cached order {OrderId} for {Seconds}s", orderId, seconds);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogWarning(ex, "Redis unavailable on SET for order {OrderId} — skipping cache write", orderId);
        }
    }

    public async Task RemoveAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        using var activity = OrderFlowDiagnostics.ActivitySource.StartActivity("cache remove");
        activity?.SetTag("cache.key", Key(orderId));

        try
        {
            await cache.RemoveAsync(Key(orderId), cancellationToken);
            logger.LogInformation("Invalidated cache entry for order {OrderId}", orderId);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogWarning(ex, "Redis unavailable on REMOVE for order {OrderId} — skipping invalidation", orderId);
        }
    }
}
