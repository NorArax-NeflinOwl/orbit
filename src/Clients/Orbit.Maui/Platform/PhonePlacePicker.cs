using Orbit.Maui.Features.Location;
using Orbit.Mobile.Location;

namespace Orbit.Maui.Platform;

/// <summary>
/// Opens the map over whatever screen asked and waits for its one answer - see
/// <see cref="PlacePickerPage"/>. Modal rather than pushed, because the reader is in the middle of
/// filling a form and is coming straight back to it.
/// </summary>
public sealed class PhonePlacePicker : IPlacePicker
{
	private readonly IServiceProvider _services;

	public PhonePlacePicker(IServiceProvider services) => _services = services;

	public async Task<PickedPlace> PickAsync(string startingAddress, CancellationToken cancellationToken = default)
	{
		if (Application.Current?.Windows.FirstOrDefault()?.Page is not { } page)
		{
			// No window to open over - nothing has gone wrong for the reader, and the form keeps
			// whatever it already held.
			return PickedPlace.Cancelled;
		}

		var picker = _services.GetRequiredService<PlacePickerPage>();
		await page.Navigation.PushModalAsync(picker);
		await picker.StartAtAsync(startingAddress);

		return await picker.Picked.WaitAsync(cancellationToken);
	}
}
