using System.Net.Http.Json;

namespace Pos.Api.Tests;

[Collection("api")]
public sealed class ReportEndpointsTests(PosApiFactory factory)
{
    [Fact]
    public async Task Summary_AfterCheckout_ReflectsFundsRaised()
    {
        var client = factory.CreateClient();
        // Buy 1 Muffin (id 2, 100c) with a fresh idempotency key.
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(new { lines = new[] { new { productId = 2, quantity = 1 } }, cashPaidCents = 100 })
        };
        req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        (await client.SendAsync(req)).EnsureSuccessStatusCode();

        var staff = await factory.CreateStaffClientAsync();
        var summary = await staff.GetFromJsonAsync<SummaryView>("/api/v1/reports/summary");
        Assert.True(summary!.TotalFundsCents >= 100);
        Assert.True(summary.OrderCount >= 1);
    }

    public sealed record SummaryView(int TotalFundsCents, int OrderCount, List<ItemView> Items);
    public sealed record ItemView(int ProductId, string Name, int QuantitySold, int RevenueCents);
}
