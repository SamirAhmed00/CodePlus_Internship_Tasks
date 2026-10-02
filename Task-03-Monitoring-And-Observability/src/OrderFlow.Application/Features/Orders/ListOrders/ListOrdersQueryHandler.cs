using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions;

namespace OrderFlow.Application.Features.Orders.ListOrders;

public class ListOrdersQueryHandler(IApplicationDbContext db, ILogger<ListOrdersQueryHandler> logger)
    : IRequestHandler<ListOrdersQuery, IReadOnlyList<OrderSummaryResponse>>
{
    public async Task<IReadOnlyList<OrderSummaryResponse>> Handle(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        var orders = await db.Orders
            .AsNoTracking()
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .Select(o => new OrderSummaryResponse(
                o.Id,
                o.CustomerName,
                o.Status.ToString(),
                o.Items.Sum(i => i.Quantity * i.UnitPrice),
                o.Items.Count))
            .ToListAsync(cancellationToken);

        logger.LogInformation("Retrieved {OrderCount} orders", orders.Count);

        return orders;
    }
}
