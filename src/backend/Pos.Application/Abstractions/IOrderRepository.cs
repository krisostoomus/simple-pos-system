using Pos.Domain.Orders;

namespace Pos.Application.Abstractions;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Order?> GetByIdempotencyKeyAsync(Guid key, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
}
