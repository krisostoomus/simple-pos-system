using Pos.Web.Services;

namespace Pos.Web.Tests;

public class CartServiceTests
{
    [Fact]
    public void Add_IncrementsQuantity_AndTotalUsesPrices()
    {
        var cart = new CartService();
        cart.Add(1); cart.Add(1); cart.Add(2);

        Assert.Equal(2, cart.QuantityOf(1));
        Assert.Equal(3, cart.Count);
        var total = cart.TotalCents(new Dictionary<int, int> { [1] = 65, [2] = 100 });
        Assert.Equal(2 * 65 + 100, total);
    }

    [Fact]
    public void Remove_DecrementsAndDropsAtZero()
    {
        var cart = new CartService();
        cart.Add(1);
        cart.Remove(1);
        Assert.Equal(0, cart.QuantityOf(1));
        Assert.True(cart.IsEmpty);
    }

    [Fact]
    public void Reset_ClearsCart()
    {
        var cart = new CartService();
        cart.Add(1); cart.Add(2);
        cart.Reset();
        Assert.True(cart.IsEmpty);
    }

    [Fact]
    public void ToLines_GroupsByProduct()
    {
        var cart = new CartService();
        cart.Add(1); cart.Add(1);
        var lines = cart.ToLines();
        var line = Assert.Single(lines);
        Assert.Equal(1, line.ProductId);
        Assert.Equal(2, line.Quantity);
    }
}
