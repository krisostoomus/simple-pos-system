using Microsoft.EntityFrameworkCore;
using Pos.Application.Abstractions;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Reporting;

public sealed class ReportQueries(PosDbContext db) : IReportQueries
{
    public async Task<ReportTotals> GetTotalsAsync(CancellationToken ct = default)
    {
        var totalFunds = await db.Orders.SumAsync(o => (int?)o.TotalCents, ct) ?? 0;
        var orderCount = await db.Orders.CountAsync(ct);
        var items = await db.OrderLines
            .GroupBy(l => l.ProductId)
            .Select(g => new ItemSold(
                g.Key,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.UnitPriceCents * x.Quantity)))
            .ToListAsync(ct);
        return new ReportTotals(totalFunds, orderCount, items);
    }
}
