namespace Pos.Application.Abstractions;

public interface IReportQueries
{
    Task<ReportTotals> GetTotalsAsync(CancellationToken ct = default);
}

public sealed record ReportTotals(int TotalFundsCents, int OrderCount, IReadOnlyList<ItemSold> Items);

public sealed record ItemSold(int ProductId, int QuantitySold, int RevenueCents);
