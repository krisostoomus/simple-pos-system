using Pos.Api.Contracts;
using Pos.Application.Catalog;

namespace Pos.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/products", async (CatalogService catalog, HttpContext http, CancellationToken ct) =>
        {
            var culture = http.Request.Headers.AcceptLanguage.ToString();
            return Results.Ok(await catalog.GetProductsAsync(culture, ct));
        })
        .WithSummary("List the catalog with live stock.");

        group.MapGet("/products/{id:int}", async (int id, CatalogService catalog, HttpContext http, CancellationToken ct) =>
        {
            var culture = http.Request.Headers.AcceptLanguage.ToString();
            var product = await catalog.GetProductAsync(id, culture, ct);
            return product is null ? Results.NotFound() : Results.Ok(product);
        });

        group.MapPut("/products/{id:int}/stock", async (int id, SetStockRequest body, CatalogService catalog, CancellationToken ct) =>
        {
            await catalog.SetStockAsync(id, body.Quantity, ct);
            return Results.NoContent();
        })
        .RequireAuthorization("staff")
        .WithSummary("Set a product's stock quantity (staff only).");

        return group;
    }
}
