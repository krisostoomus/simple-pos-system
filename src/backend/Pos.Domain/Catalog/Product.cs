using Pos.Domain.Exceptions;

namespace Pos.Domain.Catalog;

/// <summary>Catalog product. Canonical <see cref="Name"/> is the base-culture (English) name;
/// per-culture overrides live in <see cref="Translations"/> with fallback to the canonical name.</summary>
public class Product
{
    private readonly List<ProductTranslation> _translations = [];

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public ProductCategory Category { get; private set; }
    public int PriceCents { get; private set; }
    public int StockQuantity { get; private set; }
    public string ImageKey { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<ProductTranslation> Translations => _translations;
    public bool IsOutOfStock => StockQuantity <= 0;

    private Product() { } // EF

    public Product(
        string name, ProductCategory category, int priceCents,
        int stockQuantity, string imageKey, bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (priceCents < 0)
            throw new ArgumentOutOfRangeException(nameof(priceCents), "Price cannot be negative.");
        if (stockQuantity < 0)
            throw new ArgumentOutOfRangeException(nameof(stockQuantity), "Stock cannot be negative.");
        if (string.IsNullOrWhiteSpace(imageKey))
            throw new ArgumentException("Image key is required.", nameof(imageKey));

        Name = name;
        Category = category;
        PriceCents = priceCents;
        StockQuantity = stockQuantity;
        ImageKey = imageKey;
        IsActive = isActive;
    }

    public void AddTranslation(string cultureCode, string name)
    {
        var code = cultureCode.ToLowerInvariant();
        if (_translations.Any(t => t.CultureCode == code))
            throw new InvalidOperationException($"Translation for '{code}' already exists.");
        _translations.Add(new ProductTranslation(code, name));
    }

    /// <summary>Resolves the display name for a neutral culture, falling back to the canonical name.</summary>
    public string GetName(string? cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
            return Name;
        var code = cultureCode.ToLowerInvariant();
        return _translations.FirstOrDefault(t => t.CultureCode == code)?.Name ?? Name;
    }

    public bool HasSufficientStock(int quantity)
        => IsActive && quantity > 0 && StockQuantity >= quantity;

    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        if (StockQuantity < quantity)
            throw new InsufficientStockException(Id, quantity, StockQuantity);
        StockQuantity -= quantity;
    }

    public void SetStock(int quantity)
    {
        if (quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Stock cannot be negative.");
        StockQuantity = quantity;
    }
}
