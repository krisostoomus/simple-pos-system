using System.Net;
using System.Net.Http.Json;

namespace Pos.Api.Tests;

[Collection("api")]
public sealed class ProductEndpointsTests(PosApiFactory factory)
{
    [Fact]
    public async Task GetProducts_ReturnsSeededCatalog()
    {
        var client = factory.CreateClient();
        var payload = await client.GetFromJsonAsync<ProductListView>("/api/v1/products");
        Assert.NotNull(payload);
        Assert.Equal(9, payload!.Products.Count); // 5 edible + 4 second-hand
        Assert.Contains(payload.Products, p => p.Name == "Brownie" && p.PriceCents == 65);
    }

    [Fact]
    public async Task GetProducts_WithEstonianHeader_ReturnsLocalizedName()
    {
        var client = factory.CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        req.Headers.Add("Accept-Language", "et");
        var resp = await client.SendAsync(req);
        var payload = await resp.Content.ReadFromJsonAsync<ProductListView>();
        Assert.Contains(payload!.Products, p => p.Name == "Brauni"); // Brownie -> Brauni
    }

    [Fact]
    public async Task SetStock_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();
        var resp = await client.PutAsJsonAsync("/api/v1/products/6/stock", new { quantity = 10 });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task SetStock_AsStaff_UpdatesQuantity()
    {
        var client = await factory.CreateStaffClientAsync();
        var resp = await client.PutAsJsonAsync("/api/v1/products/6/stock", new { quantity = 12 });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        var payload = await client.GetFromJsonAsync<ProductListView>("/api/v1/products");
        Assert.Equal(12, payload!.Products.Single(p => p.Id == 6).StockQuantity);
    }

    public sealed record ProductView(int Id, string Name, string Category, int PriceCents, int StockQuantity, string ImageKey, bool IsOutOfStock);
    public sealed record ProductListView(IReadOnlyList<ProductView> Products);
}
