namespace Pos.Infrastructure.Seeding;

public sealed class SeedFile
{
    public List<SeedProduct> Products { get; set; } = [];
}

public sealed class SeedProduct
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";       // "Edible" | "SecondHand"
    public int PriceCents { get; set; }
    public int StockQuantity { get; set; }
    public string ImageKey { get; set; } = "";
    public Dictionary<string, string> Translations { get; set; } = new();
}
