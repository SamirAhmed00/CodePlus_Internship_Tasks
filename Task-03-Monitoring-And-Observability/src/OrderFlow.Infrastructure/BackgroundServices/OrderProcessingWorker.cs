using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Observability;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.ReadModels;
using OrderFlow.Infrastructure.Caching;
using OrderFlow.Infrastructure.Persistence;

namespace OrderFlow.Infrastructure.BackgroundServices;

public class OrderProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    OrderFlowMetrics metrics,
    ILogger<OrderProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OrderProcessingWorker started (interval: {Interval}s)",
            configuration["Background:RefreshIntervalSeconds"] ?? "30");

        while (!stoppingToken.IsCancellationRequested)
        {
            var startedAt = Stopwatch.GetTimestamp();
            var succeeded = false;

            using (var activity = OrderFlowDiagnostics.ActivitySource.StartActivity("Worker Cycle"))
            {
                try
                {
                    var cycle = await ProcessCycleAsync(stoppingToken);

                    activity?.SetTag("worker.completed_orders", cycle.CompletedCount);
                    activity?.SetTag("worker.dashboard_rows", cycle.DashboardRowCount);

                    metrics.RecordWorkerOrdersCompleted(cycle.CompletedCount);
                    succeeded = true;

                    logger.LogInformation(
                        "Worker cycle finished in {DurationMs} ms: {CompletedCount} pending order(s) completed, {DashboardRowCount} dashboard row(s)",
                        (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                        cycle.CompletedCount,
                        cycle.DashboardRowCount);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    activity?.AddException(ex);
                    logger.LogError(ex, "Background cycle failed — will retry on the next tick");
                }
            }

            metrics.RecordWorkerCycle(succeeded);

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

    private async Task<(int CompletedCount, int DashboardRowCount)> ProcessCycleAsync(CancellationToken cancellationToken)
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

        return (completedIds.Count, dashboardRows.Count);
    }
}
