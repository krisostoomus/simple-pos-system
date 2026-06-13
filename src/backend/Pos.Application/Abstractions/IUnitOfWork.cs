namespace Pos.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>Persists pending changes. Implementations MUST translate an optimistic-concurrency
    /// failure into <see cref="Pos.Application.Exceptions.ConcurrencyConflictException"/>.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
