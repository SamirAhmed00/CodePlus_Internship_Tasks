using MediatR;
using OrderFlow.Application.Abstractions;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Features.Orders.CreateOrder;

public class CreateOrderCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateOrderCommand, CreateOrderResponse>
{
    public async Task<CreateOrderResponse> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var order = Order.Create(
            command.CustomerName,
            command.Items.Select(i => new OrderItem(i.ProductName, i.Quantity, i.UnitPrice)));

        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        return new CreateOrderResponse(order.Id);
    }
}
