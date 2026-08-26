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

// Singleton, not Scoped: AddHttpMessageHandler<AuthorizationMessageHandler> below resolves its handler
// (and whatever it depends on) from IHttpClientFactory's own internal, periodically-rotating DI scope,
// not this app's single long-lived scope - a Scoped registration here would silently hand
// AuthorizationMessageHandler a throwaway TokenStore instance on every request instead of sharing the
// same one components use to read/write tokens. TokenStore itself is a stateless wrapper over
// IJSRuntime/localStorage either way, so this only matters because of what depends on it below; a
// Blazor WASM app also has no real per-request concept for "Scoped" to model in the first place.
builder.Services.AddSingleton<TokenStore>();

// A separate, unauthenticated client for TokenRefreshService's own refresh call (see its class comment
// for why that call can't go through AuthorizationMessageHandler itself).
builder.Services.AddHttpClient(tokenRefreshHttpClientName, httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress));
builder.Services.AddSingleton<TokenRefreshService>(services => new TokenRefreshService(
    services.GetRequiredService<TokenStore>(),
    services.GetRequiredService<IHttpClientFactory>().CreateClient(tokenRefreshHttpClientName)));
// Must be Transient, not Singleton: IHttpClientFactory mutates a handler's InnerHandler while
// assembling each client's pipeline, so reusing one instance across multiple pipelines (as Singleton or
// Scoped-from-the-wrong-scope would) throws "the inner handler is already set" on the second client
// that resolves it. A fresh, cheap Transient instance still resolves the same Singleton
// TokenStore/TokenRefreshService/OrbitAuthenticationStateProvider above and below through constructor
// injection, which is the part that actually needed to be shared - see this type's class remarks.
builder.Services.AddTransient<AuthorizationMessageHandler>();

// Registered once as the concrete type and forwarded from the AuthenticationStateProvider base type,
// so both injection sites resolve to the same instance. Singleton for the same reason as TokenStore
// above: AuthorizationMessageHandler calls NotifyAuthenticationStateChanged() on this when a request's
// access and refresh tokens both turn out to be dead (see its class comment) - a Scoped registration
// meant that call landed on a throwaway instance nothing was subscribed to, so MainLayout's sidebar
// never found out the session had ended until something else (a manual login/logout, or the
// session-expiry heartbeat below, both of which run in the app's real scope) eventually noticed too.
builder.Services.AddSingleton<OrbitAuthenticationStateProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider>(services => services.GetRequiredService<OrbitAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddHttpClient<NotesApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<TasksApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<InventoryApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
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
builder.Services.AddHttpClient<NotificationsApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<ClientFlagsApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress));
// Carries the token handler like the rest: making and revoking a link needs the owner's session, and
// the reader's half of this client works with or without one.
builder.Services.AddHttpClient<PublicShareApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<TransferApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddScoped<OwnEncryptionKeyProvider>();
builder.Services.AddScoped<EncryptedChatMessageSender>();
builder.Services.AddScoped<EncryptedChatMessageReader>();
builder.Services.AddScoped<SharedLocationSender>();
builder.Services.AddScoped<GoogleIntegrationAccess>();
builder.Services.AddScoped<PrivateContentSealer>();
builder.Services.AddScoped<PushNotificationManager>();
builder.Services.AddScoped<ThemeService>();
// Singleton so every page reads the same choice - MainLayout initialises it and re-renders on Changed.
builder.Services.AddSingleton<Translations>();
// Shared unread state so the avatar badge, the nav-section badges, and Chat's contact avatars all read
// the same poll (MainLayout owns it) instead of each fetching their own.
builder.Services.AddScoped<NotificationFeedState>();
builder.Services.AddScoped<ClientExceptionLog>();

// A third-party host, not Orbit.Api - deliberately not given AuthorizationMessageHandler, so Orbit's
// own bearer token is never sent to it (see GeocodingApiClient's class comment).
builder.Services.AddHttpClient<GeocodingApiClient>(
    httpClient => httpClient.BaseAddress = new Uri("https://nominatim.openstreetmap.org/"));

await builder.Build().RunAsync();
