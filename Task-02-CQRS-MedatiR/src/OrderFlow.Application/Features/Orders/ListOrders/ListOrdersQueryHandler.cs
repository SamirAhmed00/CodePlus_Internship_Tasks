using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions;

namespace OrderFlow.Application.Features.Orders.ListOrders;

public class ListOrdersQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListOrdersQuery, IReadOnlyList<OrderSummaryResponse>>
{
    public async Task<IReadOnlyList<OrderSummaryResponse>> Handle(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        return await db.Orders
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
    }
}
