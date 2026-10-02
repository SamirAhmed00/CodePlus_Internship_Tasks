using System.Text.Json;
using MediatR;
using OrderFlow.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Orders.GetOrderById;

public class GetOrderByIdQueryHandler(
    IApplicationDbContext db,
    IOrderCache cache,
    ILogger<GetOrderByIdQueryHandler> logger)
    : IRequestHandler<GetOrderByIdQuery, OrderDetailsResponse?>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OrderDetailsResponse?> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var cached = await cache.GetAsync(query.Id, cancellationToken);
        if (cached is not null)
            return JsonSerializer.Deserialize<OrderDetailsResponse>(cached, JsonOptions);

        var fromDb = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == query.Id)
            .Select(o => new OrderDetailsResponse(
                o.Id,
                o.CustomerName,
                o.Status.ToString(),
                o.Items.Sum(i => i.Quantity * i.UnitPrice),
                o.CreatedAt,
                o.CompletedAt,
                o.Items.Select(i => new OrderItemResponse(i.ProductName, i.Quantity, i.UnitPrice)).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        if (fromDb is null)
        {
            logger.LogInformation("Order {OrderId} not found", query.Id);
            return null;
        }

        await cache.SetAsync(query.Id, JsonSerializer.Serialize(fromDb, JsonOptions), cancellationToken);

        logger.LogInformation("Order {OrderId} retrieved from SQL Server", query.Id);

        return fromDb;
    }
}
