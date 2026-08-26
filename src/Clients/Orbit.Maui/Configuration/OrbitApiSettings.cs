namespace Orbit.Maui.Configuration;

/// <summary>
/// Where the app looks for the Orbit API.
///
/// The development default has to differ per platform because "the machine running the server" is not
/// the same address from each: the iOS simulator shares the Mac's loopback, while the Android emulator
/// reaches its host through the fixed alias 10.0.2.2. Neither works from a physical device, which needs
/// the Mac's address on the LAN - see src/Clients/Orbit.Maui/README.md.
/// </summary>
public sealed record OrbitApiSettings(Uri BaseAddress)
{
    public static OrbitApiSettings Development { get; } = new(new Uri(DevelopmentBaseAddress));

    private const string DevelopmentBaseAddress =
#if ANDROID
        "http://10.0.2.2:5080/";
#else
        "http://localhost:5080/";
#endif
}
