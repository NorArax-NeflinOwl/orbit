using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Orbit.Web;
using Orbit.Web.Services;
using Orbit.Web.Services.Logging;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Logging.SetMinimumLevel(LogLevel.Trace);
// Mirrors Warning-and-above log lines into localStorage (see wwwroot/js/clientLogging.js) so they can be
// retrieved on-device without devtools - see PersistentLoggerProvider's class comment.
builder.Services.AddSingleton<ILoggerProvider, PersistentLoggerProvider>();

// Read from wwwroot/appsettings.json (or appsettings.Development.json under `dotnet run`/`dotnet
// watch`, which the Blazor dev server selects automatically).
//
// wwwroot/appsettings.json (the Docker/nginx deployment) leaves this blank: nginx reverse-proxies
// /api/* to Orbit.Api under the same origin the browser loaded the page from (see nginx.conf), so the
// API is simply "wherever this page came from" - no separate host or port to compute, and no CORS
// needed since every request is same-origin. This is also what makes the app reachable from another
// device's browser via this machine's LAN IP: the browser only ever talks to the one origin it loaded
// the page from.
//
// appsettings.Development.json (the `dotnet run`/`dotnet watch` dev server) still sets a concrete
// address, since that dev server has no proxy in front of it and always runs on a fixed port
// different from Orbit.Api's - only the host is replaced with whatever host the browser used, so a
// `dotnet run` instance opened via a LAN IP still finds the right machine.
var browserOrigin = new Uri(builder.HostEnvironment.BaseAddress);
var configuredApiBaseAddressValue = builder.Configuration["ApiBaseAddress"];
var apiBaseAddress = string.IsNullOrEmpty(configuredApiBaseAddressValue)
    ? browserOrigin.ToString()
    : new UriBuilder(new Uri(configuredApiBaseAddressValue)) { Host = browserOrigin.Host }.Uri.ToString();
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
builder.Services.AddHttpClient<PushNotificationApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddScoped<OwnEncryptionKeyProvider>();
builder.Services.AddScoped<EncryptedChatMessageSender>();
builder.Services.AddScoped<PushNotificationManager>();
builder.Services.AddScoped<ThemeService>();

// A third-party host, not Orbit.Api - deliberately not given AuthorizationMessageHandler, so Orbit's
// own bearer token is never sent to it (see GeocodingApiClient's class comment).
builder.Services.AddHttpClient<GeocodingApiClient>(
    httpClient => httpClient.BaseAddress = new Uri("https://nominatim.openstreetmap.org/"));

await builder.Build().RunAsync();
