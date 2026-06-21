using Pos.Api.Contracts;
using Pos.Application.Abstractions;
using Pos.Application.Checkout;

namespace Pos.Api.Endpoints;

public static class OrderEndpoints
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder group)
    {
        group.MapPost("/orders", async (CheckoutRequestBody body, CheckoutService checkout, HttpContext http, CancellationToken ct) =>
        {
            // The key must identify the checkout *intent*, not the HTTP send — fabricating one here would
            // silently disable idempotency for any caller that omits it, so reject rather than invent.
            if (!http.Request.Headers.TryGetValue(IdempotencyHeader, out var raw)
                || !Guid.TryParse(raw.ToString(), out var key))
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "missing_idempotency_key",
                    detail: $"A '{IdempotencyHeader}' request header carrying a GUID is required for checkout.",
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "missing_idempotency_key" });
            }

            var request = new CheckoutRequest(
                body.Lines.Select(l => new CheckoutLine(l.ProductId, l.Quantity)).ToList(),
                body.CashPaidCents, key);

            var result = await checkout.CheckoutAsync(request, ct);
            return Results.Created($"/api/v1/orders/{result.OrderId}", result);
        })
        .WithSummary("Checkout: validate, take payment, decrement stock, persist the order.");

        group.MapGet("/orders/{id:int}", async (int id, IOrderRepository orders, CancellationToken ct) =>
        {
            var order = await orders.GetByIdAsync(id, ct);
            return order is null ? Results.NotFound() : Results.Ok(OrderMapper.ToResult(order));
        });

        return group;
    }
}
