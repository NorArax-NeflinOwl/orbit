using System.Reflection;

namespace Orbit.Maui.Configuration;

/// <summary>
/// Where the web client of this deployment is, which is where the documents Orbit publishes about
/// itself are served from - see OrbitDocumentLinks, and the About screen that lists them.
///
/// Separate from <see cref="OrbitApiSettings"/> because the two addresses are genuinely different on
/// Azure: the phone talks to orbit-api directly and the privacy, security and documentation pages are
/// routes of orbit-web. Deriving one from the other would send every one of those rows to a host that
/// serves an API and no pages.
///
/// Baked into the build the same way, and optional in the same way. A build told nothing has no web
/// address, and the About screen leaves those rows out rather than offering links that go nowhere:
///
/// <code>dotnet build ... -p:OrbitWebBaseAddress=https://orbit-web.example.azurecontainerapps.io/</code>
/// </summary>
public sealed record OrbitWebSettings(Uri? BaseAddress)
{
    private const string MetadataKey = "OrbitWebBaseAddress";

    public static OrbitWebSettings Current { get; } = new(DeployedBaseAddress());

    /// <summary>
    /// Null when the build said nothing, or said something that is not an absolute http(s) address -
    /// the same rule OrbitApiSettings applies, and for the same reason: a bad value is a build mistake
    /// and must not take the app down on its first screen.
    /// </summary>
    private static Uri? DeployedBaseAddress()
    {
        if (!Uri.TryCreate(Metadata(), UriKind.Absolute, out var address)
            || address.Scheme is not ("http" or "https"))
        {
            return null;
        }

        // A relative path resolves against the base by replacing its last segment, so an address
        // without a trailing slash loses one - see OrbitApiSettings, where the same bite was found.
        return address.AbsoluteUri.EndsWith('/') ? address : new Uri(address.AbsoluteUri + "/");
    }

    private static string? Metadata()
        => typeof(OrbitWebSettings).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == MetadataKey)
            ?.Value;
}
