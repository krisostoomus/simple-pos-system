using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Pos.Web.Models;

namespace Pos.Web.Services;

/// <summary>Typed wrapper over the POS REST API. Sends Accept-Language for localized names and the
/// caller's Idempotency-Key per checkout, and attaches the staff bearer token when present.</summary>
public sealed class PosApiClient(HttpClient http, AuthState auth, CultureService culture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ProductModel>> GetProductsAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/products");
        req.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture.Current));
        using var resp = await http.SendAsync(req, ct);
        await EnsureSuccess(resp);
        return (await resp.Content.ReadFromJsonAsync<List<ProductModel>>(Json, ct))!;
    }

    // The idempotency key is supplied by the caller (not minted here) so it stays stable across retries
    // and manual re-submits of the *same* checkout — that stability is what lets the server dedupe.
    public async Task<CheckoutResultModel> CheckoutAsync(CheckoutBody body, Guid idempotencyKey, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/orders")
        {
            Content = JsonContent.Create(body, options: Json),
        };
        req.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        using var resp = await http.SendAsync(req, ct);
        await EnsureSuccess(resp);
        return (await resp.Content.ReadFromJsonAsync<CheckoutResultModel>(Json, ct))!;
    }

    public async Task<bool> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        using var resp = await http.PostAsJsonAsync("api/v1/auth/token", new TokenBody(username, password), Json, ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized) return false;
        await EnsureSuccess(resp);
        var token = await resp.Content.ReadFromJsonAsync<TokenResult>(Json, ct);
        auth.SetToken(token!.AccessToken);
        return true;
    }

    public async Task<ReportSummaryModel> GetReportSummaryAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/reports/summary");
        req.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture.Current));
        if (auth.Token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        using var resp = await http.SendAsync(req, ct);
        await EnsureSuccess(resp);
        return (await resp.Content.ReadFromJsonAsync<ReportSummaryModel>(Json, ct))!;
    }

    public async Task SetStockAsync(int productId, int quantity, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, $"api/v1/products/{productId}/stock")
        {
            Content = JsonContent.Create(new SetStockBody(quantity), options: Json),
        };
        if (auth.Token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        using var resp = await http.SendAsync(req, ct);
        await EnsureSuccess(resp);
    }

    private static async Task EnsureSuccess(HttpResponseMessage resp)
    {
        if (resp.IsSuccessStatusCode) return;
        string errorCode = "error";
        string? detail = null;
        try
        {
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            if (doc.RootElement.TryGetProperty("errorCode", out var ec)) errorCode = ec.GetString() ?? errorCode;
            if (doc.RootElement.TryGetProperty("detail", out var d)) detail = d.GetString();
        }
        catch { /* non-JSON body */ }
        throw new ApiException((int)resp.StatusCode, errorCode, detail);
    }
}
