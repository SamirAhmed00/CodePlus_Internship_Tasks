using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.ReadModels;
using OrderFlow.Infrastructure.Caching;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.BackgroundServices;

public class OrderProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<OrderProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OrderProcessingWorker started (interval: {Interval}s)",
            configuration["Background:RefreshIntervalSeconds"] ?? "30");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background cycle failed — will retry on the next tick");
            }

            try
            {
                var seconds = int.TryParse(configuration["Background:RefreshIntervalSeconds"], out var parsed) && parsed > 0
                    ? parsed
                    : 30;

                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("OrderProcessingWorker stopped");
    }

    private async Task ProcessCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IOrderCache>();

        var pendingOrders = await db.Orders
            .Where(o => o.Status == OrderStatus.Pending)
            .ToListAsync(cancellationToken);

        var completedIds = new List<Guid>();

        foreach (var order in pendingOrders)
        {
            order.Complete();
            completedIds.Add(order.Id);
        }

        if (completedIds.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Completed {Count} pending order(s)", completedIds.Count);

            foreach (var id in completedIds)
                await cache.RemoveAsync(id, cancellationToken);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.OrderDashboards.ExecuteDeleteAsync(cancellationToken);

        var dashboardRows = await db.Orders
            .AsNoTracking()
            .Select(o => new OrderDashboard
            {
                OrderId = o.Id,
                CustomerName = o.CustomerName,
                ItemCount = o.Items.Count,
                Total = o.Items.Sum(i => i.Quantity * i.UnitPrice),
                Status = o.Status,
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(cancellationToken);

        db.OrderDashboards.AddRange(dashboardRows);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("OrderDashboard refreshed with {Count} row(s)", dashboardRows.Count);
    }
}
