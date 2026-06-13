namespace Pos.Application.Exceptions;

public sealed class InvalidQuantityException : Exception
{
    public int ProductId { get; }
    public int Quantity { get; }

    public InvalidQuantityException(int productId, int quantity)
        : base($"Invalid quantity {quantity} for product {productId}.")
    {
        ProductId = productId;
        Quantity = quantity;
    }
}
