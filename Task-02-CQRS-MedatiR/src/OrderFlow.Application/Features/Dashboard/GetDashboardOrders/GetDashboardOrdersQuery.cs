using MediatR;

namespace OrderFlow.Application.Features.Dashboard.GetDashboardOrders;

public record GetDashboardOrdersQuery() : IRequest<IReadOnlyList<DashboardOrderResponse>>;
