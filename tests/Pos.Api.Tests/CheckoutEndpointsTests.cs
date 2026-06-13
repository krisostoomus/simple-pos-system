using System.Net;
using System.Net.Http.Json;

namespace Pos.Api.Tests;

[Collection("api")]
public sealed class CheckoutEndpointsTests(PosApiFactory factory)
{
    private static HttpRequestMessage Checkout(object body, Guid? key = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders") { Content = JsonContent.Create(body) };
        if (key is not null) req.Headers.Add("Idempotency-Key", key.ToString());
        return req;
    }

    [Fact]
    public async Task Checkout_WithOverpayment_Returns201AndChange()
    {
        var client = factory.CreateClient();
        var resp = await client.SendAsync(Checkout(new
        {
            lines = new[] { new { productId = 1, quantity = 2 } }, // 2 × Brownie = 130
            cashPaidCents = 200
        }, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var result = await resp.Content.ReadFromJsonAsync<CheckoutView>();
        Assert.Equal(130, result!.TotalCents);
        Assert.Equal(70, result.ChangeCents);
    }

    [Fact]
    public async Task Checkout_BeyondStock_Returns409OutOfStock()
    {
        var client = factory.CreateClient();
        var resp = await client.SendAsync(Checkout(new
        {
            lines = new[] { new { productId = 3, quantity = 100000 } }, // far beyond Cake Pop stock
            cashPaidCents = 100000000
        }, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<ProblemView>();
        Assert.Equal("out_of_stock", problem!.ErrorCode);
    }

    [Fact]
    public async Task Checkout_InsufficientPayment_Returns422()
    {
        var client = factory.CreateClient();
        var resp = await client.SendAsync(Checkout(new
        {
            lines = new[] { new { productId = 1, quantity = 1 } }, // 65
            cashPaidCents = 10
        }, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resp.StatusCode);
        var problem = await resp.Content.ReadFromJsonAsync<ProblemView>();
        Assert.Equal("insufficient_payment", problem!.ErrorCode);
    }

    [Fact]
    public async Task Checkout_EmptyCart_Returns400()
    {
        var client = factory.CreateClient();
        var resp = await client.SendAsync(Checkout(new { lines = Array.Empty<object>(), cashPaidCents = 0 }, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Checkout_SameIdempotencyKey_CreatesSingleOrder()
    {
        var client = factory.CreateClient();
        var key = Guid.NewGuid();
        var body = new { lines = new[] { new { productId = 1, quantity = 1 } }, cashPaidCents = 100 };

        var first = await client.SendAsync(Checkout(body, key));
        var second = await client.SendAsync(Checkout(body, key));

        var firstResult = await first.Content.ReadFromJsonAsync<CheckoutView>();
        var secondResult = await second.Content.ReadFromJsonAsync<CheckoutView>();
        Assert.Equal(firstResult!.OrderId, secondResult!.OrderId); // replay returns the same order
    }

    [Fact]
    public async Task Checkout_ConcurrentSameIdempotencyKey_NoDuplicateAndNo500()
    {
        var client = factory.CreateClient();
        var key = Guid.NewGuid();
        var body = new { lines = new[] { new { productId = 1, quantity = 1 } }, cashPaidCents = 100 };

        // Two genuinely concurrent submits of the same key (the double-submit race).
        var responses = await Task.WhenAll(
            client.SendAsync(Checkout(body, key)),
            client.SendAsync(Checkout(body, key)));

        // Neither returns 500; both resolve to the same single order (one created, one replayed).
        Assert.All(responses, r =>
            Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
                $"unexpected status {(int)r.StatusCode}"));

        var ids = new List<int>();
        foreach (var r in responses)
            ids.Add((await r.Content.ReadFromJsonAsync<CheckoutView>())!.OrderId);
        Assert.Equal(ids[0], ids[1]);
    }

    public sealed record CheckoutView(int OrderId, int TotalCents, int CashPaidCents, int ChangeCents);
    public sealed record ProblemView(
        [property: System.Text.Json.Serialization.JsonPropertyName("errorCode")] string ErrorCode);
}
