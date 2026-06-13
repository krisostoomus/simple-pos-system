using System.Net;
using System.Net.Http.Json;

namespace Pos.Api.Tests;

[Collection("api")]
public sealed class ConcurrencyTests(PosApiFactory factory)
{
    [Fact]
    public async Task ParallelCheckoutsOnLastItem_ExactlyOneSucceeds()
    {
        // Arrange: set a known product (id 7, Pants) to exactly 1 in stock as staff.
        var staff = await factory.CreateStaffClientAsync();
        (await staff.PutAsJsonAsync("/api/v1/products/7/stock", new { quantity = 1 }))
            .EnsureSuccessStatusCode();

        // Act: fire N concurrent checkouts each buying 1 of product 7.
        const int n = 8;
        var client = factory.CreateClient();
        var tasks = Enumerable.Range(0, n).Select(async _ =>
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
            {
                Content = JsonContent.Create(new
                {
                    lines = new[] { new { productId = 7, quantity = 1 } },
                    cashPaidCents = 300
                })
            };
            req.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            return await client.SendAsync(req);
        }).ToArray();

        var responses = await Task.WhenAll(tasks);

        // Assert: exactly one 201, the rest 409.
        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflict = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, created);
        Assert.Equal(n - 1, conflict);
    }
}
