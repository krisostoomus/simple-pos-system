using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Exceptions;
using Pos.Domain.Exceptions;

namespace Pos.Api.Errors;

/// <summary>Maps domain/application exceptions to RFC 9457 ProblemDetails with a machine-readable
/// errorCode (and productId where relevant), so the API stays language-neutral.</summary>
public sealed class ProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, code, productId) = Map(ex);
        if (status is null)
            return false; // unhandled -> default 500 pipeline

        ctx.Response.StatusCode = status.Value;
        var problem = new ProblemDetails
        {
            Status = status.Value,
            Title = code,
            Detail = ex.Message,
        };
        problem.Extensions["errorCode"] = code;
        if (productId is not null)
            problem.Extensions["productId"] = productId;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            Exception = ex,
            ProblemDetails = problem,
        });
    }

    private static (int? status, string code, int? productId) Map(Exception ex) => ex switch
    {
        EmptyCartException => (StatusCodes.Status400BadRequest, "empty_cart", null),
        InvalidQuantityException e => (StatusCodes.Status400BadRequest, "invalid_quantity", e.ProductId),
        UnknownProductException e => (StatusCodes.Status400BadRequest, "unknown_product", e.ProductId),
        ProductNotFoundException e => (StatusCodes.Status404NotFound, "not_found", e.ProductId),
        InsufficientStockException e => (StatusCodes.Status409Conflict, "out_of_stock", e.ProductId),
        ConcurrencyConflictException => (StatusCodes.Status409Conflict, "concurrency_conflict", null),
        InsufficientPaymentException => (StatusCodes.Status422UnprocessableEntity, "insufficient_payment", null),
        _ => (null, "internal_error", null),
    };
}
