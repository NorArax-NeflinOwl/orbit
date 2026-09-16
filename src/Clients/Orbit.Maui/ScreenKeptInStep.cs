using Orbit.Mobile.Sync;

namespace Orbit.Maui;

/// <summary>
/// Redraws a screen from the phone's own store when a sync nobody on that screen asked for has brought
/// something down - see <see cref="PeriodicSync"/>, which is what runs while somebody sits on a screen
/// they have not touched.
///
/// Held by the page rather than by the view model, for two reasons. The page has a lifecycle to attach
/// and let go on: a view model is built per screen and nothing disposes it, so one that listened would
/// go on redrawing lists nobody is looking at, one more of them for every screen ever opened. And the
/// redraw has to happen on the UI thread - the timer's does not run there - which Orbit.Mobile has no
/// way to say, having no MAUI to say it with.
///
/// The redraw reads the store and asks the server nothing. Running the screen's own load instead would
/// mean a second full synchronisation behind every tick, which is the work this is reacting to.
/// </summary>
public sealed class ScreenKeptInStep : IDisposable
{
	private readonly SyncState _syncState;
	private readonly Func<Task> _redraw;

	public ScreenKeptInStep(SyncState syncState, Func<Task> redraw)
	{
		_syncState = syncState;
		_redraw = redraw;
	}

	/// <summary>From the screen's OnAppearing. Listening twice would redraw twice, so it is let go of first.</summary>
	public void Listen()
	{
		StopListening();
		_syncState.BroughtSomethingNew += OnBroughtSomethingNew;
	}

	/// <summary>From the screen's OnDisappearing - see the class comment on why this half matters.</summary>
	public void StopListening() => _syncState.BroughtSomethingNew -= OnBroughtSomethingNew;

	public void Dispose() => StopListening();

	private void OnBroughtSomethingNew(object? sender, EventArgs e)
		=> MainThread.BeginInvokeOnMainThread(() => _ = _redraw());
}
