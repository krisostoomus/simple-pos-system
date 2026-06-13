namespace Pos.Application.Exceptions;

/// <summary>Raised when an order insert violates the unique idempotency-key constraint — i.e. a
/// concurrent request carrying the same key already created the order. The checkout flow resolves
/// this by returning the winning order (idempotent replay).</summary>
public sealed class DuplicateIdempotencyKeyException : Exception
{
    public DuplicateIdempotencyKeyException(string message = "Duplicate idempotency key.") : base(message) { }
}
