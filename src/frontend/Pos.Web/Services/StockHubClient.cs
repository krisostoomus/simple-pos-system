using Microsoft.AspNetCore.SignalR.Client;

namespace Pos.Web.Services;

/// <summary>Subscribes to the API's /hubs/stock and raises an event when a product's stock changes.</summary>
public sealed class StockHubClient(HttpClient http) : IAsyncDisposable
{
    private HubConnection? _connection;

    /// <summary>Fired with (productId, newQuantity) on every broadcast.</summary>
    public event Action<int, int>? StockChanged;

    public async Task StartAsync()
    {
        if (_connection is not null) return;
        var hubUrl = new Uri(http.BaseAddress!, "hubs/stock");
        _connection = new HubConnectionBuilder().WithUrl(hubUrl).WithAutomaticReconnect().Build();
        _connection.On<StockChangedDto>("StockChanged", dto => StockChanged?.Invoke(dto.ProductId, dto.NewQuantity));
        await _connection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }

    private sealed record StockChangedDto(int ProductId, int NewQuantity);
}
