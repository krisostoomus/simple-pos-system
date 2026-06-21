using System.Text.Json;

namespace Pos.Api.Tests;

[Collection("api")]
public sealed class OpenApiDocumentTests(PosApiFactory factory)
{
    [Fact]
    public async Task OpenApiDocument_SubstitutesVersionInPaths()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));

        var paths = doc.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();

        // The version must be substituted into the URL, not left as the literal route template.
        Assert.DoesNotContain(paths, p => p.Contains("{version}"));
        Assert.Contains("/api/v1/products", paths);
    }

    [Fact]
    public async Task GetProducts_DocumentsAcceptLanguageHeader()
    {
        var client = factory.CreateClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));

        var parameters = doc.RootElement
            .GetProperty("paths").GetProperty("/api/v1/products")
            .GetProperty("get").GetProperty("parameters").EnumerateArray();

        Assert.Contains(parameters, p =>
            p.GetProperty("name").GetString() == "Accept-Language" &&
            p.GetProperty("in").GetString() == "header");
    }
}
