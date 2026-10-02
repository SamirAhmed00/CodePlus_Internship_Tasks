namespace OrderFlow.Application.Features.Orders.ListOrders;

public record OrderSummaryResponse(Guid Id, string CustomerName, string Status, decimal Total, int ItemCount);
