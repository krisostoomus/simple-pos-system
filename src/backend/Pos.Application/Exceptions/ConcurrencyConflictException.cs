namespace Pos.Application.Exceptions;

/// <summary>Raised when a persistence optimistic-concurrency conflict cannot be resolved.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message = "A concurrency conflict occurred.")
        : base(message) { }
}
