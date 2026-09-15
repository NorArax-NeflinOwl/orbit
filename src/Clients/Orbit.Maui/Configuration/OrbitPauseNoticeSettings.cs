using System.Reflection;

namespace Orbit.Maui.Configuration;

/// <summary>
/// Where the deployment leaves word that it is paused on purpose - the file
/// <c>scripts/stop-azure-compute.sh</c> writes when the cost ceiling stops the server, and removes when
/// it is resumed. Read by <see cref="Orbit.Mobile.Sync.PauseNoticeReader"/>, and only after the API has
/// failed to answer as itself; see info/orbit-maui-plan.md, "Living without the server".
///
/// Baked into the build like <see cref="OrbitApiSettings"/>, and optional like
/// <see cref="OrbitWebSettings"/>: a build told nothing never reports a pause, and reads a stopped
/// server as it always did - as offline in everything but the network.
///
/// <code>dotnet publish ... -p:OrbitPauseNoticeAddress=https://orbitdownloads.blob.core.windows.net/apps/status.json</code>
/// </summary>
public sealed record OrbitPauseNoticeSettings(Uri? Address)
{
    private const string MetadataKey = "OrbitPauseNoticeAddress";

    public static OrbitPauseNoticeSettings Current { get; } = new(DeployedAddress());

    /// <summary>
    /// Null when the build said nothing, or said something that is not an absolute http(s) address -
    /// the rule the other two settings apply, for the same reason: a bad value is a build mistake and
    /// must not take the app down on its first screen.
    /// </summary>
    private static Uri? DeployedAddress()
        => Uri.TryCreate(Metadata(), UriKind.Absolute, out var address) && address.Scheme is "http" or "https"
            ? address
            : null;

    private static string? Metadata()
        => typeof(OrbitPauseNoticeSettings).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == MetadataKey)
            ?.Value;
}
