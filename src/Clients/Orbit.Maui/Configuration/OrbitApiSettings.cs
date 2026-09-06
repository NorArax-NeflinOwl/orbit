using System.Reflection;

namespace Orbit.Maui.Configuration;

/// <summary>
/// Where the app looks for the Orbit API.
///
/// Baked into the build rather than configured on the app, because it has to be known before anything
/// can be asked - there is no server to ask where the server is. A build told nothing looks for a
/// development server on the machine that produced it; a build meant for somebody to install is told
/// the deployment's own address:
///
/// <code>dotnet build ... -p:OrbitApiBaseAddress=https://orbit-web.example.azurecontainerapps.io/</code>
///
/// That is orbit-api's own address. It was orbit-web's for as long as the API had internal ingress only,
/// and the detour cost two things: every sync woke orbit-web, which is set to scale to zero and so never
/// did, and the phone was coupled to a deploy of a client it does not use. Every client here asks for a
/// relative path - "api/notes" and the like - so one base address serves the whole app either way.
///
/// The browser still goes through nginx, because a same-origin /api/ is what keeps it free of CORS.
///
/// The development default has to differ per platform because "the machine running the server" is not
/// the same address from each: the iOS simulator shares the development machine's loopback, while the
/// Android emulator reaches its host through the fixed alias 10.0.2.2. Neither works from a physical
/// device, which needs the machine's address on the LAN - see src/Clients/Orbit.Maui/README.md.
///
/// The development port is 5080 unless the build was told otherwise. It has to be overridable for the
/// same reason the address is baked in at all: two people working on one machine - an Android head and
/// an iOS one, say - can only run one Orbit.Api between them otherwise, since whichever server took
/// 5080 first is the one both apps reach and the second is running for nobody.
///
/// <code>dotnet build ... -p:OrbitDevelopmentApiPort=5099</code>
/// </summary>
public sealed record OrbitApiSettings(Uri BaseAddress)
{
    /// <summary>What the port is when the build said nothing, and what every ordinary build uses.</summary>
    private const int DefaultDevelopmentPort = 5080;

    /// <summary>
    /// The names the csproj writes into the assembly - see its properties of the same names. Read from
    /// metadata rather than through preprocessor constants because both values are strings a build
    /// supplies, which #if cannot carry.
    /// </summary>
    private const string BaseAddressMetadataKey = "OrbitApiBaseAddress";
    private const string PortMetadataKey = "OrbitDevelopmentApiPort";

    /// <summary>
    /// The deployment this build was made for, or the development server when it was made for nobody in
    /// particular.
    /// </summary>
    public static OrbitApiSettings Current { get; } =
        new(DeployedBaseAddress() ?? new Uri(DevelopmentBaseAddress()));

    /// <summary>
    /// Null when the build said nothing, or said something that is not an absolute http(s) address.
    /// Falling back rather than failing for the same reason the port does: an app that cannot reach its
    /// server says so on its first screen, where a crash on startup says nothing anybody can act on.
    /// </summary>
    private static Uri? DeployedBaseAddress()
    {
        if (!Uri.TryCreate(Metadata(BaseAddressMetadataKey), UriKind.Absolute, out var address)
            || address.Scheme is not ("http" or "https"))
        {
            return null;
        }

        // HttpClient resolves a relative path against the base address by replacing its last segment, so
        // an address without a trailing slash loses one - "api/notes" against "https://host/orbit" asks
        // for "https://host/api/notes".
        return address.AbsoluteUri.EndsWith('/') ? address : new Uri(address.AbsoluteUri + "/");
    }

    private static string DevelopmentBaseAddress() =>
#if ANDROID
        $"http://10.0.2.2:{DevelopmentPort()}/";
#else
        $"http://localhost:{DevelopmentPort()}/";
#endif

    private static int DevelopmentPort()
        => int.TryParse(Metadata(PortMetadataKey), out var port) && port is > 0 and <= 65535
            ? port
            : DefaultDevelopmentPort;

    private static string? Metadata(string key)
        => typeof(OrbitApiSettings).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == key)
            ?.Value;
}
