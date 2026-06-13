namespace Pos.Application.Exceptions;

public sealed class UnknownProductException : Exception
{
    public int ProductId { get; }

    public UnknownProductException(int productId)
        : base($"Unknown product {productId}.")
    {
        ProductId = productId;
    }
}
