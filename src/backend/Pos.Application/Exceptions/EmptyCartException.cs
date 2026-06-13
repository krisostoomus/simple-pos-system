namespace Pos.Application.Exceptions;

public sealed class EmptyCartException : Exception
{
    public EmptyCartException() : base("The cart is empty.") { }
}
