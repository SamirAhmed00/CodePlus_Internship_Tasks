using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Observability;

public sealed class OrderFlowMetrics
{
    private static readonly double[] DurationBuckets =
        [0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 10];

    private readonly IServiceScopeFactory _scopeFactory;

    private readonly Counter<long> _httpRequests;
    private readonly Histogram<double> _httpRequestDuration;
    private readonly Counter<long> _httpErrors;
    private readonly Counter<long> _ordersCreated;
    private readonly Counter<long> _workerCycles;
    private readonly Counter<long> _workerOrdersCompleted;

    public OrderFlowMetrics(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;

        var meter = new Meter(OrderFlowDiagnostics.MeterName);

        _httpRequests = meter.CreateCounter<long>(
            "orderflow.http.requests",
            unit: "{request}",
            description: "Number of HTTP requests handled by the API.");

        _httpRequestDuration = meter.CreateHistogram<double>(
            "orderflow.http.request.duration",
            unit: "s",
            description: "Duration of HTTP requests handled by the API.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = DurationBuckets });

        _httpErrors = meter.CreateCounter<long>(
            "orderflow.http.errors",
            unit: "{error}",
            description: "Number of HTTP responses with status code 400 or higher.");

        _ordersCreated = meter.CreateCounter<long>(
            "orderflow.orders.created",
            unit: "{order}",
            description: "Number of orders created through the API.");

        meter.CreateObservableGauge(
            "orderflow.orders.pending",
            ObservePendingOrders,
            unit: "{order}",
            description: "Number of orders currently in the Pending status.");

        _workerCycles = meter.CreateCounter<long>(
            "orderflow.worker.cycles",
            unit: "{cycle}",
            description: "Number of background worker processing cycles.");

        _workerOrdersCompleted = meter.CreateCounter<long>(
            "orderflow.worker.orders.completed",
            unit: "{order}",
            description: "Number of pending orders completed by the background worker.");
    }

    public void RecordHttpRequest(string method, string route, int statusCode, TimeSpan duration)
    {
        var statusTags = new TagList
        {
            { "http.request.method", method },
            { "http.route", route },
            { "http.response.status_code", statusCode }
        };

        _httpRequests.Add(1, statusTags);
        _httpRequestDuration.Record(duration.TotalSeconds, new TagList
        {
            { "http.request.method", method },
            { "http.route", route }
        });

        if (statusCode >= 400)
            _httpErrors.Add(1, statusTags);
    }

    public void RecordOrderCreated() => _ordersCreated.Add(1);

    public void RecordWorkerCycle(bool succeeded) =>
        _workerCycles.Add(1, new TagList { { "outcome", succeeded ? "success" : "error" } });

    public void RecordWorkerOrdersCompleted(int count)
    {
        if (count > 0)
            _workerOrdersCompleted.Add(count);
    }

    // Reported from a synchronous metrics callback, so the scoped count runs with a timeout
    // and emits no measurement while the database is unavailable.
    private IEnumerable<Measurement<long>> ObservePendingOrders()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            var pending = scope.ServiceProvider
                .GetRequiredService<IApplicationDbContext>()
                .Orders
                .Where(o => o.Status == OrderStatus.Pending)
                .CountAsync(timeout.Token)
                .GetAwaiter()
                .GetResult();

            return [new Measurement<long>(pending)];
        }
        catch (Exception)
        {
            return [];
        }
    }
}
