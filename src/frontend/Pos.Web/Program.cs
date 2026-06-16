using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Pos.Web;
using Pos.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

builder.Services.AddMudServices();
builder.Services.AddLocalization();
builder.Services.AddSingleton<AuthState>();
builder.Services.AddScoped<CultureService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<PosApiClient>();
builder.Services.AddScoped<StockHubClient>();

var host = builder.Build();

// Apply the persisted culture before the app renders.
var culture = host.Services.GetRequiredService<CultureService>();
await culture.InitializeAsync();

await host.RunAsync();
