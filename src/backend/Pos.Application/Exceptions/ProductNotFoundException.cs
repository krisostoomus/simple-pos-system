namespace Pos.Application.Exceptions;

public sealed class ProductNotFoundException : Exception
{
    public int ProductId { get; }

    public ProductNotFoundException(int productId)
        : base($"Product {productId} was not found.")
    {
        ProductId = productId;
    }
}
