using Pos.Application.Abstractions;
using Pos.Application.Exceptions;
using Pos.Domain.Catalog;

namespace Pos.Application.Catalog;

public sealed class CatalogService
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;

    public CatalogService(IProductRepository products, IUnitOfWork uow)
    {
        _products = products;
        _uow = uow;
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? culture, CancellationToken ct = default)
    {
        var products = await _products.GetAllActiveAsync(ct);
        var neutral = CultureHelper.ToNeutral(culture);
        return products.Select(p => ToDto(p, neutral)).ToList();
    }

    public async Task<ProductDto?> GetProductAsync(int id, string? culture, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(id, ct);
        return product is null ? null : ToDto(product, CultureHelper.ToNeutral(culture));
    }

    public async Task SetStockAsync(int id, int quantity, CancellationToken ct = default)
    {
        if (quantity < 0)
            throw new InvalidQuantityException(id, quantity);

        var product = await _products.GetByIdAsync(id, ct)
            ?? throw new ProductNotFoundException(id);

        product.SetStock(quantity);
        await _uow.SaveChangesAsync(ct);
    }

    private static ProductDto ToDto(Product p, string? neutralCulture)
        => new(p.Id, p.GetName(neutralCulture), p.Category.ToString(),
               p.PriceCents, p.StockQuantity, p.ImageKey, p.IsOutOfStock);
}
