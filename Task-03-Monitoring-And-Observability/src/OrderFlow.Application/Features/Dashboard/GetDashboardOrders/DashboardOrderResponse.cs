namespace OrderFlow.Application.Features.Dashboard.GetDashboardOrders;

public record DashboardOrderResponse(
    Guid OrderId,
    string CustomerName,
    int ItemCount,
    decimal Total,
    string Status);
