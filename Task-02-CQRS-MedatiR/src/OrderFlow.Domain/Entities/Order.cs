using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.Entities;

public class Order
{
    private readonly List<OrderItem> _items = [];

    private Order()
    {
    }

    public static Order Create(string customerName, IEnumerable<OrderItem> items)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new OrderDomainException("Customer name is required.");

        var materialized = items?.ToList() ?? throw new OrderDomainException("An order must contain at least one item.");

        if (materialized.Count == 0)
            throw new OrderDomainException("An order must contain at least one item.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerName = customerName.Trim(),
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in materialized)
            order.AddItem(item.ProductName, item.Quantity, item.UnitPrice);

        return order;
    }

    public Guid Id { get; private set; }

    public string CustomerName { get; private set; } = null!;

    public OrderStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public decimal Total => _items.Sum(i => i.LineTotal);

    public void AddItem(string productName, int quantity, decimal unitPrice)
    {
        if (Status != OrderStatus.Pending)
            throw new OrderDomainException("Items can only be added to a Pending order.");

        _items.Add(new OrderItem(productName, quantity, unitPrice));
    }

    public void Complete()
    {
        if (Status != OrderStatus.Pending)
            throw new OrderDomainException($"Only Pending orders can be completed. Current status: {Status}.");

        Status = OrderStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }
}
