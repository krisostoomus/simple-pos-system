using Pos.Domain.Orders;
using Pos.Domain.Payments;

namespace Pos.Application.Checkout;

public sealed record CheckoutLine(int ProductId, int Quantity);

public sealed record CheckoutRequest(
    IReadOnlyList<CheckoutLine> Lines, int CashPaidCents, Guid IdempotencyKey);

public sealed record OrderLineDto(
    int ProductId, string ProductName, int UnitPriceCents, int Quantity, int LineTotalCents);

public sealed record ChangePieceDto(int DenominationCents, int Count);

public sealed record CheckoutResult(
    int OrderId, int TotalCents, int CashPaidCents, int ChangeCents,
    IReadOnlyList<ChangePieceDto> Change, IReadOnlyList<OrderLineDto> Lines, DateTime CreatedAtUtc);

/// <summary>Projects a persisted <see cref="Order"/> into the API result shape, recomputing the
/// change breakdown deterministically. Shared by checkout and the order-detail endpoint.</summary>
public static class OrderMapper
{
    public static CheckoutResult ToResult(Order order)
    {
        var change = ChangeCalculator.Calculate(order.ChangeCents)
            .Select(p => new ChangePieceDto(p.DenominationCents, p.Count))
            .ToList();
        var lines = order.Lines
            .Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.UnitPriceCents, l.Quantity, l.LineTotalCents))
            .ToList();
        return new CheckoutResult(
            order.Id, order.TotalCents, order.CashPaidCents, order.ChangeCents,
            change, lines, order.CreatedAtUtc);
    }
}
