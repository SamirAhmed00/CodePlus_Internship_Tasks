using OrderFlow.Domain.Enums;

namespace OrderFlow.Domain.ReadModels;

public class OrderDashboard
{
    public Guid OrderId { get; set; }

    public string CustomerName { get; set; } = null!;

    public int ItemCount { get; set; }

    public decimal Total { get; set; }

    public OrderStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }
}
