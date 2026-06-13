namespace Pos.Domain.Exceptions;

/// <summary>Base type for violations of domain invariants.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}
