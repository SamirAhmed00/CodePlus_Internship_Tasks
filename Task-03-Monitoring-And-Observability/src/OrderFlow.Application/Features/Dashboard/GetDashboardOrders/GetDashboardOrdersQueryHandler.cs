using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions;

namespace OrderFlow.Application.Features.Dashboard.GetDashboardOrders;

public class GetDashboardOrdersQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDashboardOrdersQuery, IReadOnlyList<DashboardOrderResponse>>
{
    public async Task<IReadOnlyList<DashboardOrderResponse>> Handle(
        GetDashboardOrdersQuery query, CancellationToken cancellationToken)
    {
        return await db.OrderDashboards
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DashboardOrderResponse(
                d.OrderId,
                d.CustomerName,
                d.ItemCount,
                d.Total,
                d.Status.ToString()))
            .ToListAsync(cancellationToken);
    }
}
