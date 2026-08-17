using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Orbit.Web;
using Orbit.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Logging.SetMinimumLevel(LogLevel.Trace);

// Read from wwwroot/appsettings.json (or appsettings.Development.json under `dotnet run`/`dotnet
// watch`, which the Blazor dev server selects automatically). This runs in the browser, so it must
// point at an address the browser can reach - never a docker-compose service name.
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "https://localhost:7080/";
const string tokenRefreshHttpClientName = "Orbit.Web.TokenRefresh";

builder.Services.AddScoped<TokenStore>();

// A separate, unauthenticated client for AuthorizationMessageHandler's own token-refresh call (see its
// class comment for why that call can't go through AuthorizationMessageHandler itself).
builder.Services.AddHttpClient(tokenRefreshHttpClientName, httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress));
builder.Services.AddScoped<AuthorizationMessageHandler>(services => new AuthorizationMessageHandler(
    services.GetRequiredService<TokenStore>(),
    services.GetRequiredService<IHttpClientFactory>().CreateClient(tokenRefreshHttpClientName)));

// Registered once as the concrete type and forwarded from the AuthenticationStateProvider base type,
// so both injection sites resolve to the same scoped instance.
builder.Services.AddScoped<OrbitAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<OrbitAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();

builder.Services.AddHttpClient<NotesApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<TasksApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<CalendarApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<AuthApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<UsersApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<ChatApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddScoped<OwnEncryptionKeyProvider>();
builder.Services.AddScoped<EncryptedChatMessageSender>();

// A third-party host, not Orbit.Api - deliberately not given AuthorizationMessageHandler, so Orbit's
// own bearer token is never sent to it (see GeocodingApiClient's class comment).
builder.Services.AddHttpClient<GeocodingApiClient>(
    httpClient => httpClient.BaseAddress = new Uri("https://nominatim.openstreetmap.org/"));

await builder.Build().RunAsync();
