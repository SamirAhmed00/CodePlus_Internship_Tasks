using MediatR;

namespace OrderFlow.Application.Features.Orders.CreateOrder;

public record CreateOrderCommand(string CustomerName, IReadOnlyList<OrderItemInput> Items)
    : IRequest<CreateOrderResponse>;

public record OrderItemInput(string ProductName, int Quantity, decimal UnitPrice);

public record CreateOrderResponse(Guid Id);
