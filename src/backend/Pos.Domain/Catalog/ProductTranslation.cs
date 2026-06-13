namespace Pos.Domain.Catalog;

/// <summary>A localized product name for one neutral culture (e.g. "et").</summary>
public class ProductTranslation
{
    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public string CultureCode { get; private set; } = null!;
    public string Name { get; private set; } = null!;

    private ProductTranslation() { } // EF

    public ProductTranslation(string cultureCode, string name)
    {
        if (string.IsNullOrWhiteSpace(cultureCode))
            throw new ArgumentException("Culture code is required.", nameof(cultureCode));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        CultureCode = cultureCode.ToLowerInvariant();
        Name = name;
    }
}
