using Pos.Domain.Catalog;
using Pos.Domain.Exceptions;

namespace Pos.Domain.Tests;

public class ProductTests
{
    private static Product NewBrownie(int stock = 10) =>
        new("Brownie", ProductCategory.Edible, priceCents: 65, stockQuantity: stock, imageKey: "brownie");

    [Fact]
    public void Constructor_WithBlankName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new Product(" ", ProductCategory.Edible, 65, 10, "brownie"));
    }

    [Fact]
    public void Constructor_WithNegativeStock_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Product("Brownie", ProductCategory.Edible, 65, -1, "brownie"));
    }

    [Fact]
    public void IsOutOfStock_WhenZero_IsTrue()
    {
        Assert.True(NewBrownie(stock: 0).IsOutOfStock);
        Assert.False(NewBrownie(stock: 1).IsOutOfStock);
    }

    [Fact]
    public void GetName_WithNoTranslation_FallsBackToCanonical()
    {
        var product = NewBrownie();
        Assert.Equal("Brownie", product.GetName("et"));
        Assert.Equal("Brownie", product.GetName(null));
    }

    [Fact]
    public void GetName_WithTranslation_ReturnsLocalizedName()
    {
        var product = NewBrownie();
        product.AddTranslation("et", "Brauni");

        Assert.Equal("Brauni", product.GetName("et"));
        Assert.Equal("Brauni", product.GetName("ET"));
        Assert.Equal("Brownie", product.GetName("en"));
    }

    [Fact]
    public void AddTranslation_DuplicateCulture_Throws()
    {
        var product = NewBrownie();
        product.AddTranslation("et", "Brauni");
        Assert.Throws<InvalidOperationException>(() => product.AddTranslation("et", "Muu"));
    }

    [Fact]
    public void HasSufficientStock_RespectsQuantityAndActiveFlag()
    {
        var product = NewBrownie(stock: 3);
        Assert.True(product.HasSufficientStock(3));
        Assert.False(product.HasSufficientStock(4));
        Assert.False(product.HasSufficientStock(0));
    }

    [Fact]
    public void DecreaseStock_ReducesQuantity()
    {
        var product = NewBrownie(stock: 5);
        product.DecreaseStock(2);
        Assert.Equal(3, product.StockQuantity);
    }

    [Fact]
    public void DecreaseStock_BeyondAvailable_ThrowsInsufficientStock()
    {
        var product = NewBrownie(stock: 1);
        var ex = Assert.Throws<InsufficientStockException>(() => product.DecreaseStock(2));
        Assert.Equal(2, ex.Requested);
        Assert.Equal(1, ex.Available);
    }

    [Fact]
    public void SetStock_Negative_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewBrownie().SetStock(-1));
    }

    [Fact]
    public void SetStock_SetsAbsoluteQuantity()
    {
        var product = NewBrownie(stock: 0);
        product.SetStock(25);
        Assert.Equal(25, product.StockQuantity);
    }
}
