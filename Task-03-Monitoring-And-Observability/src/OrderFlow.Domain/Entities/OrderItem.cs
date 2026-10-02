using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.Entities;

public class OrderItem
{
    private OrderItem()
    {
    }

    public OrderItem(string productName, int quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new OrderDomainException("Product name is required.");

        if (quantity <= 0)
            throw new OrderDomainException("Quantity must be greater than zero.");

        if (unitPrice < 0)
            throw new OrderDomainException("Unit price cannot be negative.");

        Id = Guid.NewGuid();
        ProductName = productName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public string ProductName { get; private set; } = null!;

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
