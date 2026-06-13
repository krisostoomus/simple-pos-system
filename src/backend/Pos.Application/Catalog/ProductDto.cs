namespace Pos.Application.Catalog;

public sealed record ProductDto(
    int Id, string Name, string Category, int PriceCents,
    int StockQuantity, string ImageKey, bool IsOutOfStock);
