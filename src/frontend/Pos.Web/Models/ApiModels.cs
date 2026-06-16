namespace Pos.Web.Models;

public sealed record ProductModel(
    int Id, string Name, string Category, int PriceCents,
    int StockQuantity, string ImageKey, bool IsOutOfStock);

public sealed record ChangePieceModel(int DenominationCents, int Count);

public sealed record OrderLineModel(
    int ProductId, string ProductName, int UnitPriceCents, int Quantity, int LineTotalCents);

public sealed record CheckoutResultModel(
    int OrderId, int TotalCents, int CashPaidCents, int ChangeCents,
    IReadOnlyList<ChangePieceModel> Change, IReadOnlyList<OrderLineModel> Lines, DateTime CreatedAtUtc);

public sealed record CheckoutLineModel(int ProductId, int Quantity);
public sealed record CheckoutBody(IReadOnlyList<CheckoutLineModel> Lines, int CashPaidCents);
public sealed record SetStockBody(int Quantity);
public sealed record TokenBody(string Username, string Password);
public sealed record TokenResult(string AccessToken, DateTime ExpiresAtUtc);
public sealed record ReportItemModel(int ProductId, string Name, int QuantitySold, int RevenueCents);
public sealed record ReportSummaryModel(int TotalFundsCents, int OrderCount, IReadOnlyList<ReportItemModel> Items);

/// <summary>Thrown by the API client on a non-success response; carries the API errorCode.</summary>
public sealed class ApiException(int statusCode, string errorCode, string? detail)
    : Exception($"API {statusCode}: {errorCode}")
{
    public int StatusCode { get; } = statusCode;
    public string ErrorCode { get; } = errorCode;
    public string? Detail { get; } = detail;
}
