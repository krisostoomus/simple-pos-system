namespace Pos.Application.Reporting;

public sealed record ReportSummaryDto(int TotalFundsCents, int OrderCount, IReadOnlyList<ItemSoldDto> Items);

public sealed record ItemSoldDto(int ProductId, string Name, int QuantitySold, int RevenueCents);
