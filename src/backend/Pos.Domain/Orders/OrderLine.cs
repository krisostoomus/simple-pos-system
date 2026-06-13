namespace Pos.Domain.Orders;

/// <summary>A purchased line. Name and unit price are snapshotted at sale time so historical
/// orders stay correct if the product is later renamed or repriced.</summary>
public class OrderLine
{
    public int Id { get; private set; }
    public int OrderId { get; private set; }
    public int ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public int UnitPriceCents { get; private set; }
    public int Quantity { get; private set; }

    public int LineTotalCents => UnitPriceCents * Quantity;

    private OrderLine() { } // EF

    public OrderLine(int productId, string productName, int unitPriceCents, int quantity)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name is required.", nameof(productName));
        if (unitPriceCents < 0)
            throw new ArgumentOutOfRangeException(nameof(unitPriceCents), "Price cannot be negative.");
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");

        ProductId = productId;
        ProductName = productName;
        UnitPriceCents = unitPriceCents;
        Quantity = quantity;
    }
}
