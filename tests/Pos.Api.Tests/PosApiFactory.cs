using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;

namespace Pos.Api.Tests;

public sealed class PosApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("pos").WithUsername("pos").WithPassword("pos")
        .Build();

    public async Task InitializeAsync() => await _db.StartAsync();

    public new async Task DisposeAsync() => await _db.DisposeAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTest");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _db.GetConnectionString(),
                ["Jwt:SigningKey"] = "integration-test-signing-key-32bytes-minimum!!",
                ["StaffCredential:Username"] = "staff",
                ["StaffCredential:Password"] = "test-password",
                ["RunMigrationsOnStartup"] = "true",
            });
        });
    }

    /// <summary>Authenticates as staff and returns an HttpClient with the bearer token set.</summary>
    public async Task<HttpClient> CreateStaffClientAsync()
    {
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("/api/v1/auth/token",
            new { username = "staff", password = "test-password" });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", body!.AccessToken);
        return client;
    }

    public sealed record TokenResponse(string AccessToken, DateTime ExpiresAtUtc);
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<PosApiFactory>;
