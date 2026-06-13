using Microsoft.EntityFrameworkCore;
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
    }
}
