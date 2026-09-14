using MediatR;

namespace OrderFlow.Application.Features.Orders.GetOrderById;

public record GetOrderByIdQuery(Guid Id) : IRequest<OrderDetailsResponse?>;
