using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Domain.Catalog;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Repositories;

public sealed class ProductRepository(PosDbContext db) : IProductRepository
{
    public async Task<IReadOnlyList<Product>> GetAllActiveAsync(CancellationToken ct = default)
        => await db.Products.AsNoTracking().Include(p => p.Translations)
            .Where(p => p.IsActive).OrderBy(p => p.Id).ToListAsync(ct);

    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
        => await db.Products.Include(p => p.Translations).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
        => await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
}
