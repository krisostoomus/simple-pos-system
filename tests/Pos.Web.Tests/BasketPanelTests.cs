using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Pos.Web.Components;
using Pos.Web.Models;

namespace Pos.Web.Tests;

public class BasketPanelTests : BunitContext
{
    public BasketPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddMudServices();
    }

    private static ProductModel Product(int id, int price = 100, int stock = 5)
        => new(id, $"Item {id}", "Edible", price, stock, "brownie");

    private static IReadOnlyList<BasketLine> Lines(params BasketLine[] lines) => lines;

    [Fact]
    public void RendersOneRowPerLine()
    {
        var lines = Lines(
            new BasketLine(Product(1), 2),
            new BasketLine(Product(2), 1));

        var cut = Render<BasketPanel>(p => p.Add(c => c.Lines, lines));

        Assert.Equal(2, cut.FindAll(".pos-basket-line").Count);
    }

    [Fact]
    public void EmptyLines_RendersNoRowsAndNoHandle()
    {
        var cut = Render<BasketPanel>(p => p.Add(c => c.Lines, Lines()));

        Assert.Empty(cut.FindAll(".pos-basket-line"));
        Assert.Empty(cut.FindAll(".pos-basket-handle"));
    }

    [Fact]
    public void IncrementButton_InvokesOnAddWithProductId()
    {
        int? added = null;
        var cut = Render<BasketPanel>(p => p
            .Add(c => c.Lines, Lines(new BasketLine(Product(7), 1)))
            .Add(c => c.OnAdd, id => added = id));

        cut.Find(".pos-basket-inc").Click();

        Assert.Equal(7, added);
    }

    [Fact]
    public void DecrementButton_InvokesOnRemoveWithProductId()
    {
        int? removed = null;
        var cut = Render<BasketPanel>(p => p
            .Add(c => c.Lines, Lines(new BasketLine(Product(7), 2)))
            .Add(c => c.OnRemove, id => removed = id));

        cut.Find(".pos-basket-dec").Click();

        Assert.Equal(7, removed);
    }

    [Fact]
    public void RemoveButton_InvokesOnRemoveLineWithProductId()
    {
        int? removedLine = null;
        var cut = Render<BasketPanel>(p => p
            .Add(c => c.Lines, Lines(new BasketLine(Product(7), 3)))
            .Add(c => c.OnRemoveLine, id => removedLine = id));

        cut.Find(".pos-basket-remove").Click();

        Assert.Equal(7, removedLine);
    }

    [Fact]
    public void IncrementButton_DisabledWhenQuantityAtStockCap()
    {
        // quantity 5 == stock 5 => cannot add more
        var cut = Render<BasketPanel>(p => p
            .Add(c => c.Lines, Lines(new BasketLine(Product(7, stock: 5), 5))));

        Assert.True(cut.Find(".pos-basket-inc").HasAttribute("disabled"));
    }
}
