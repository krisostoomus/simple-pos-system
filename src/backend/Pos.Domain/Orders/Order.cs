using Pos.Domain.Exceptions;

namespace Pos.Domain.Orders;

/// <summary>A completed sale. Created via <see cref="Create"/>; the canonical price total is
/// derived from the lines and the payment invariant (cash ≥ total) is enforced here.</summary>
public class Order
{
    private readonly List<OrderLine> _lines = [];

    public int Id { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public int TotalCents { get; private set; }
    public int CashPaidCents { get; private set; }
    public int ChangeCents { get; private set; }
    public Guid IdempotencyKey { get; private set; }

    public IReadOnlyCollection<OrderLine> Lines => _lines;

    private Order() { } // EF

    private Order(
        IEnumerable<OrderLine> lines, int cashPaidCents, int changeCents,
        Guid idempotencyKey, DateTime createdAtUtc)
    {
        _lines.AddRange(lines);
        if (_lines.Count == 0)
            throw new ArgumentException("Order must have at least one line.", nameof(lines));

        TotalCents = _lines.Sum(l => l.LineTotalCents);
        if (cashPaidCents < TotalCents)
            throw new InsufficientPaymentException(TotalCents, cashPaidCents);
        if (changeCents != cashPaidCents - TotalCents)
            throw new ArgumentException("Change must equal cash paid minus total.", nameof(changeCents));

        CashPaidCents = cashPaidCents;
        ChangeCents = changeCents;
        IdempotencyKey = idempotencyKey;
        CreatedAtUtc = createdAtUtc;
    }

    public static Order Create(
        IEnumerable<OrderLine> lines, int cashPaidCents, int changeCents,
        Guid idempotencyKey, DateTime createdAtUtc)
        => new(lines, cashPaidCents, changeCents, idempotencyKey, createdAtUtc);
}
