using Pos.Application.Abstractions;

namespace Pos.Application.Reporting;

public sealed class ReportingService
{
    private readonly IReportQueries _queries;
    private readonly IProductRepository _products;

    public ReportingService(IReportQueries queries, IProductRepository products)
    {
        _queries = queries;
        _products = products;
    }

    public async Task<ReportSummaryDto> GetSummaryAsync(string? culture, CancellationToken ct = default)
    {
        var totals = await _queries.GetTotalsAsync(ct);
        var products = await _products.GetAllActiveAsync(ct);
        var byId = products.ToDictionary(p => p.Id);
        var neutral = string.IsNullOrWhiteSpace(culture) ? null : culture.Split('-')[0].ToLowerInvariant();

        var items = totals.Items
            .Select(i => new ItemSoldDto(
                i.ProductId,
                byId.TryGetValue(i.ProductId, out var p) ? p.GetName(neutral) : $"#{i.ProductId}",
                i.QuantitySold,
                i.RevenueCents))
            .ToList();

        return new ReportSummaryDto(totals.TotalFundsCents, totals.OrderCount, items);
    }
}
