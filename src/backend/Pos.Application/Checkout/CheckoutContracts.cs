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
