namespace Pos.Web.Models;

/// <summary>Product category values as serialized by the API (mirrors the domain enum names).</summary>
public static class ProductCategories
{
    public const string Edible = "Edible";
    public const string SecondHand = "SecondHand";
}

public sealed record ProductModel(
    int Id, string Name, string Category, int PriceCents, int StockQuantity, string ImageKey)
{
    /// <summary>Derived from stock — no need to keep a separate flag in sync. (The API also sends an
    /// <c>isOutOfStock</c> field, which is simply ignored on deserialization.)</summary>
    public bool IsOutOfStock => StockQuantity <= 0;
}

/// <summary>The catalog response: items live under a <c>products</c> property rather than a bare
/// top-level array, matching the API's collection-response convention.</summary>
public sealed record ProductListModel(IReadOnlyList<ProductModel> Products);

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
