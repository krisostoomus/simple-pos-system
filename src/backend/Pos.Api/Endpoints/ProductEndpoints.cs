using Microsoft.AspNetCore.Mvc;
using Pos.Api.Contracts;
using Pos.Application.Catalog;
using Pos.Infrastructure.Auth;

namespace Pos.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapGet("/products", async (CatalogService catalog, [FromHeader(Name = "Accept-Language")] string? acceptLanguage, CancellationToken ct) =>
            Results.Ok(new ProductListResponse(await catalog.GetProductsAsync(acceptLanguage, ct))))
        .WithSummary("List the catalog with live stock. Product names are localized via the Accept-Language header (falls back to English).");

        group.MapGet("/products/{id:int}", async (int id, CatalogService catalog, [FromHeader(Name = "Accept-Language")] string? acceptLanguage, CancellationToken ct) =>
        {
            var product = await catalog.GetProductAsync(id, acceptLanguage, ct);
            return product is null ? Results.NotFound() : Results.Ok(product);
        });

        group.MapPut("/products/{id:int}/stock", async (int id, SetStockRequest body, CatalogService catalog, CancellationToken ct) =>
        {
            await catalog.SetStockAsync(id, body.Quantity, ct);
            return Results.NoContent();
        })
        .RequireAuthorization(TokenService.StaffRole)
        .WithSummary("Set a product's stock quantity (staff only).");

        return group;
    }
}
