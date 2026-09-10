using Orbit.Mobile.Location;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// The phone's own maps app, which no test can open. What a test can check is that the screen asked, and
/// with which point - see IMapHandoff.
/// </summary>
internal sealed class RecordingMapHandoff : IMapHandoff
{
    public List<(double Latitude, double Longitude, string Label)> Shown { get; } = [];

    public Task ShowAsync(
        double latitude, double longitude, string label, CancellationToken cancellationToken = default)
    {
        Shown.Add((latitude, longitude, label));
        return Task.CompletedTask;
    }
}
