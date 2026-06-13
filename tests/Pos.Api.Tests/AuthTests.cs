using System.Net;
using System.Net.Http.Json;

namespace Pos.Api.Tests;

[Collection("api")]
public sealed class AuthTests(PosApiFactory factory)
{
    [Fact]
    public async Task Token_WithBadCredentials_Returns401()
    {
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/auth/token", new { username = "staff", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Reports_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/reports/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Reports_AsStaff_Returns200()
    {
        var client = await factory.CreateStaffClientAsync();
        var resp = await client.GetAsync("/api/v1/reports/summary");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
