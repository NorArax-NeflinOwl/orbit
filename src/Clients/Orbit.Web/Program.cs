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
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBaseAddress) });
builder.Services.AddScoped<NotesApiClient>();

await builder.Build().RunAsync();
