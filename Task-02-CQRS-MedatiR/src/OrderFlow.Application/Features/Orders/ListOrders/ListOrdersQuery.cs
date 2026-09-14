using MediatR;

namespace OrderFlow.Application.Features.Orders.ListOrders;

public record ListOrdersQuery() : IRequest<IReadOnlyList<OrderSummaryResponse>>;
