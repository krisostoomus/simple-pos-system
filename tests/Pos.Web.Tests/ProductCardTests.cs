using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using MudBlazor.Services;
using Pos.Web.Components;
using Pos.Web.Models;
using Pos.Web.Resources;

namespace Pos.Web.Tests;

public class ProductCardTests : BunitContext, IAsyncLifetime
{
    public ProductCardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddMudServices();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    [Fact]
    public void OutOfStockProduct_RendersDisabledAndDoesNotRaiseAdd()
    {
        var raised = 0;
        var product = new ProductModel(1, "Brownie", "Edible", 65, 0, "brownie", IsOutOfStock: true);
        var cut = Render<ProductCard>(p => p
            .Add(c => c.Product, product)
            .Add(c => c.OnAddToCart, _ => raised++));

        Assert.Contains("pos-disabled", cut.Markup);
        cut.Find(".mud-card").Click();
        Assert.Equal(0, raised); // disabled card swallows the click
    }

    [Fact]
    public void InStockProduct_RaisesAddOnClick()
    {
        var raised = 0;
        var product = new ProductModel(1, "Brownie", "Edible", 65, 5, "brownie", IsOutOfStock: false);
        var cut = Render<ProductCard>(p => p
            .Add(c => c.Product, product)
            .Add(c => c.OnAddToCart, _ => raised++));

        cut.Find(".mud-card").Click();
        Assert.Equal(1, raised);
    }
}
