using Pos.Application.Abstractions;
using Pos.Application.Exceptions;
using Pos.Domain.Exceptions;
using Pos.Domain.Orders;

namespace Pos.Application.Checkout;

/// <summary>Orchestrates a checkout: validation, payment, transactional stock decrement with a
/// single optimistic-concurrency retry, persistence, and live stock notification.</summary>
public sealed class CheckoutService
{
    private const int MaxAttempts = 2;

    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;
    private readonly IUnitOfWork _uow;
    private readonly IPaymentService _payment;
    private readonly IStockNotifier _notifier;
    private readonly TimeProvider _clock;

    public CheckoutService(
        IProductRepository products, IOrderRepository orders, IUnitOfWork uow,
        IPaymentService payment, IStockNotifier notifier, TimeProvider clock)
    {
        _products = products;
        _orders = orders;
        _uow = uow;
        _payment = payment;
        _notifier = notifier;
        _clock = clock;
    }

    public async Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CancellationToken ct = default)
    {
        if (request.Lines is null || request.Lines.Count == 0)
            throw new EmptyCartException();
        foreach (var line in request.Lines)
            if (line.Quantity <= 0)
                throw new InvalidQuantityException(line.ProductId, line.Quantity);

        var existing = await _orders.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
        if (existing is not null)
            return OrderMapper.ToResult(existing);

        var quantities = request.Lines
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await ProcessAsync(quantities, request, ct);
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                // Reload-and-retry: the next ProcessAsync loads fresh product state.
            }
            catch (DuplicateIdempotencyKeyException)
            {
                // A concurrent request with the same key won the race; return its order (replay).
                var winner = await _orders.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
                if (winner is not null)
                    return OrderMapper.ToResult(winner);
                throw;
            }
        }
    }

    private async Task<CheckoutResult> ProcessAsync(
        IReadOnlyDictionary<int, int> quantities, CheckoutRequest request, CancellationToken ct)
    {
        var products = await _products.GetByIdsAsync(quantities.Keys.ToArray(), ct);
        var byId = products.ToDictionary(p => p.Id);

        foreach (var productId in quantities.Keys)
            if (!byId.ContainsKey(productId))
                throw new UnknownProductException(productId);

        foreach (var (productId, quantity) in quantities)
            if (!byId[productId].HasSufficientStock(quantity))
                throw new InsufficientStockException(productId, quantity, byId[productId].StockQuantity);

        var lines = quantities
            .Select(kv => new OrderLine(kv.Key, byId[kv.Key].Name, byId[kv.Key].PriceCents, kv.Value))
            .ToList();
        var total = lines.Sum(l => l.LineTotalCents);

        var payment = _payment.AcceptCash(total, request.CashPaidCents);

        foreach (var (productId, quantity) in quantities)
            byId[productId].DecreaseStock(quantity);

        var order = Order.Create(
            lines, request.CashPaidCents, payment.ChangeCents,
            request.IdempotencyKey, _clock.GetUtcNow().UtcDateTime);

        await _orders.AddAsync(order, ct);
        await _uow.SaveChangesAsync(ct);

        foreach (var productId in quantities.Keys)
            await _notifier.NotifyStockChangedAsync(productId, byId[productId].StockQuantity, ct);

        return OrderMapper.ToResult(order);
    }
}
