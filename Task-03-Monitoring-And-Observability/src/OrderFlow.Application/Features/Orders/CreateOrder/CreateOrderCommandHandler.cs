using MediatR;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions;
using OrderFlow.Application.Observability;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Features.Orders.CreateOrder;

public class CreateOrderCommandHandler(
    IApplicationDbContext db,
    ILogger<CreateOrderCommandHandler> logger,
    OrderFlowMetrics metrics)
    : IRequestHandler<CreateOrderCommand, CreateOrderResponse>
{
    public async Task<CreateOrderResponse> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var order = Order.Create(
            command.CustomerName,
            command.Items.Select(i => new OrderItem(i.ProductName, i.Quantity, i.UnitPrice)));

        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        metrics.RecordOrderCreated();
        logger.LogInformation(
            "Order {OrderId} created for customer {CustomerName} with {ItemCount} item(s)",
            order.Id, order.CustomerName, order.Items.Count);

        return new CreateOrderResponse(order.Id);
    }
}
