using NSubstitute;
using Pos.Application.Abstractions;
using Pos.Application.Catalog;
using Pos.Application.Exceptions;

namespace Pos.Application.Tests;

public class CatalogServiceTests
{
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private CatalogService CreateSut() => new(_products, _uow);

    [Fact]
    public async Task GetProducts_LocalizesNamesWithFallback()
    {
        var brownie = TestData.Product(1, "Brownie");
        brownie.AddTranslation("et", "Brauni");
        var muffin = TestData.Product(2, "Muffin");
        _products.GetAllActiveAsync().Returns([brownie, muffin]);

        var et = await CreateSut().GetProductsAsync("et-EE");

        Assert.Equal("Brauni", et.Single(p => p.Id == 1).Name);
        Assert.Equal("Muffin", et.Single(p => p.Id == 2).Name);
    }

    [Fact]
    public async Task GetProducts_NullCulture_UsesCanonicalNames()
    {
        var brownie = TestData.Product(1, "Brownie");
        brownie.AddTranslation("et", "Brauni");
        _products.GetAllActiveAsync().Returns([brownie]);

        var result = await CreateSut().GetProductsAsync(null);

        Assert.Equal("Brownie", result.Single().Name);
    }

    [Fact]
    public async Task GetProduct_NotFound_ReturnsNull()
    {
        _products.GetByIdAsync(42).Returns((Pos.Domain.Catalog.Product?)null);
        Assert.Null(await CreateSut().GetProductAsync(42, "en"));
    }

    [Fact]
    public async Task SetStock_OnMissingProduct_Throws()
    {
        _products.GetByIdAsync(42).Returns((Pos.Domain.Catalog.Product?)null);
        await Assert.ThrowsAsync<ProductNotFoundException>(() => CreateSut().SetStockAsync(42, 5));
    }

    [Fact]
    public async Task SetStock_Negative_ThrowsInvalidQuantity()
    {
        _products.GetByIdAsync(1).Returns(TestData.Product(1));
        await Assert.ThrowsAsync<InvalidQuantityException>(() => CreateSut().SetStockAsync(1, -1));
    }

    [Fact]
    public async Task SetStock_Valid_UpdatesAndSaves()
    {
        var product = TestData.Product(1, stock: 0);
        _products.GetByIdAsync(1).Returns(product);

        await CreateSut().SetStockAsync(1, 25);

        Assert.Equal(25, product.StockQuantity);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
