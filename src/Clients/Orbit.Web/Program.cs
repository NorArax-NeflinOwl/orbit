using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Orbit.Web;
using Orbit.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Lowest level: log everything, including user interactions, to the browser console.
builder.Logging.SetMinimumLevel(LogLevel.Trace);

// Read from wwwroot/appsettings.json (or appsettings.Development.json under `dotnet run`/`dotnet
// watch`, which the Blazor dev server selects automatically). This runs in the browser, so it must
// point at an address the browser can reach - never a docker-compose service name.
var apiBaseAddress = builder.Configuration["ApiBaseAddress"] ?? "https://localhost:7080/";

builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<AuthorizationMessageHandler>();

// Registered once as the concrete type and forwarded from the AuthenticationStateProvider base type,
// so both injection sites resolve to the same scoped instance.
builder.Services.AddScoped<OrbitAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<OrbitAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();

builder.Services.AddHttpClient<NotesApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<TasksApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();
builder.Services.AddHttpClient<AuthApiClient>(httpClient => httpClient.BaseAddress = new Uri(apiBaseAddress))
    .AddHttpMessageHandler<AuthorizationMessageHandler>();

await builder.Build().RunAsync();
