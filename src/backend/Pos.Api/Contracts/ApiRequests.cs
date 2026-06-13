namespace Pos.Api.Contracts;

public sealed record SetStockRequest(int Quantity);
public sealed record CheckoutLineRequest(int ProductId, int Quantity);
public sealed record CheckoutRequestBody(IReadOnlyList<CheckoutLineRequest> Lines, int CashPaidCents);
public sealed record TokenRequest(string Username, string Password);
