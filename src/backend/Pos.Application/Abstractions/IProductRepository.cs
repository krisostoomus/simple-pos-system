using Pos.Domain.Catalog;

namespace Pos.Application.Abstractions;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllActiveAsync(CancellationToken ct = default);
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);
}
