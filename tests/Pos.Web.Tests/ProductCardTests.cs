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
        var product = new ProductModel(1, "Brownie", "Edible", 65, 0, "brownie"); // stock 0 => out of stock
        var cut = Render<ProductCard>(p => p
            .Add(c => c.Product, product)
            .Add(c => c.OnAddToCart, _ => raised++));

        Assert.Contains("pos-card--disabled", cut.Markup);
        cut.Find(".pos-card").Click();
        Assert.Equal(0, raised); // disabled card swallows the click
    }

    [Fact]
    public void InStockProduct_RaisesAddOnClick()
    {
        var raised = 0;
        var product = new ProductModel(1, "Brownie", "Edible", 65, 5, "brownie"); // stock 5 => in stock
        var cut = Render<ProductCard>(p => p
            .Add(c => c.Product, product)
            .Add(c => c.OnAddToCart, _ => raised++));

        cut.Find(".pos-card").Click();
        Assert.Equal(1, raised);
    }

    [Fact]
    public void Card_IsANativeButton_ForKeyboardActivation()
    {
        // A real <button> is focusable and activates on Enter/Space natively (no custom key handling),
        // so the card is reachable and addable with the keyboard alone — not only by mouse/touch.
        var product = new ProductModel(1, "Brownie", "Edible", 65, 5, "brownie");
        var cut = Render<ProductCard>(p => p
            .Add(c => c.Product, product)
            .Add(c => c.OnAddToCart, _ => { }));

        var card = cut.Find(".pos-card");
        Assert.Equal("BUTTON", card.TagName);
        Assert.Equal("button", card.GetAttribute("type"));
    }
}
