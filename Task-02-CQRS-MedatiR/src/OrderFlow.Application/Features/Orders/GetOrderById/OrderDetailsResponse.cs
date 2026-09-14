namespace OrderFlow.Application.Features.Orders.GetOrderById;

public record OrderItemResponse(string ProductName, int Quantity, decimal UnitPrice);

public record OrderDetailsResponse(
    Guid Id,
    string CustomerName,
    string Status,
    decimal Total,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    IReadOnlyList<OrderItemResponse> Items);
