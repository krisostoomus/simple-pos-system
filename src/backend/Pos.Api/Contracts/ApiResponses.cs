using Pos.Application.Catalog;

namespace Pos.Api.Contracts;

/// <summary>Wraps the catalog as an object so the response is extensible (paging, metadata) without a
/// breaking change — collection endpoints never return a bare top-level JSON array.</summary>
public sealed record ProductListResponse(IReadOnlyList<ProductDto> Products);
