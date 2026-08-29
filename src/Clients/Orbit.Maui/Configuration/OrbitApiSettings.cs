using System.Reflection;

namespace Orbit.Maui.Configuration;

/// <summary>
/// Where the app looks for the Orbit API.
///
/// The development default has to differ per platform because "the machine running the server" is not
/// the same address from each: the iOS simulator shares the development machine's loopback, while the
/// Android emulator reaches its host through the fixed alias 10.0.2.2. Neither works from a physical
/// device, which needs the machine's address on the LAN - see src/Clients/Orbit.Maui/README.md.
///
/// The port is 5080 unless the build was told otherwise. It has to be overridable because it is baked
/// into the app rather than configured on it: two people working on one machine - an Android head and an
/// iOS one, say - can only run one Orbit.Api between them otherwise, since whichever server took 5080
/// first is the one both apps reach and the second is running for nobody.
///
/// <code>dotnet build ... -p:OrbitDevelopmentApiPort=5099</code>
/// </summary>
public sealed record OrbitApiSettings(Uri BaseAddress)
{
    /// <summary>What the port is when the build said nothing, and what every ordinary build uses.</summary>
    private const int DefaultDevelopmentPort = 5080;

    /// <summary>
    /// The name the csproj writes into the assembly - see its OrbitDevelopmentApiPort property. Read
    /// from metadata rather than through a preprocessor constant because the value is a number inside a
    /// string, which #if cannot build.
    /// </summary>
    private const string PortMetadataKey = "OrbitDevelopmentApiPort";

    public static OrbitApiSettings Development { get; } = new(new Uri(DevelopmentBaseAddress()));

    private static string DevelopmentBaseAddress() =>
#if ANDROID
        $"http://10.0.2.2:{DevelopmentPort()}/";
#else
        $"http://localhost:{DevelopmentPort()}/";
#endif

    /// <summary>
    /// Falls back to the default rather than failing when the metadata is missing or unreadable: a wrong
    /// port shows up immediately as an app that cannot reach its server, where a crash on startup would
    /// be a far worse answer to a build-time knob nobody has touched.
    /// </summary>
    private static int DevelopmentPort()
    {
        var configured = typeof(OrbitApiSettings).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == PortMetadataKey)
            ?.Value;

        return int.TryParse(configured, out var port) && port is > 0 and <= 65535 ? port : DefaultDevelopmentPort;
    }
}
