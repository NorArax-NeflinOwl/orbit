using Orbit.Mobile.Location;

namespace Orbit.Maui.Platform;

/// <summary>
/// Opens the phone's own maps app at a point - see <see cref="IMapHandoff"/> for why that is the answer
/// rather than a map inside Orbit.
/// </summary>
public sealed class PhoneMapHandoff : IMapHandoff
{
	public async Task ShowAsync(
		double latitude, double longitude, string label, CancellationToken cancellationToken = default)
	{
		try
		{
			await Map.Default.OpenAsync(latitude, longitude, new MapLaunchOptions { Name = label });
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			// A phone with no maps app at all, or one that refused. Nothing useful to say and nothing to
			// undo: the reader pressed a button and no map opened, which they can see. Swallowed rather
			// than surfaced because every caller would otherwise have to catch it to say the same thing.
		}
	}
}
