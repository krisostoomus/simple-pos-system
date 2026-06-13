using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pos.Application.Abstractions;
using Pos.Application.Exceptions;

namespace Pos.Infrastructure.Persistence;

public sealed class UnitOfWork(PosDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex.Message);
        }
        catch (DbUpdateException ex) when (IsIdempotencyKeyViolation(ex))
        {
            // A concurrent request with the same idempotency key won the race and committed first.
            throw new DuplicateIdempotencyKeyException(ex.Message);
        }
    }

    private static bool IsIdempotencyKeyViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
           && pg.ConstraintName is not null
           && pg.ConstraintName.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase);
}
