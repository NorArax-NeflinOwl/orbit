using Orbit.Mobile.Presence;
using Orbit.Mobile.Screens.Navigation;

namespace Orbit.Maui.Controls;

/// <summary>
/// The bar across the top of every signed-in page.
///
/// Resolves its own view model rather than taking one from the page it sits in. A page's view model has
/// nothing to do with the chrome around it, and threading a second one through every page's constructor
/// - and every page's test - to hold the same six navigation commands would be worse than this.
/// </summary>
public partial class NavigationBar : ContentView
{
	/// <summary>
	/// How often the dot re-asks whether the reader has gone idle. Well under the minute that counts as
	/// idle, so the change shows up promptly, and rare enough to cost nothing.
	/// </summary>
	private static readonly TimeSpan IdleCheckInterval = TimeSpan.FromSeconds(15);

	private readonly NavigationBarViewModel _viewModel;
	private readonly Presence _presence;
	private IDispatcherTimer? _idleTimer;

	public NavigationBar()
	{
		InitializeComponent();
		var services = IPlatformApplication.Current!.Services;
		_viewModel = services.GetRequiredService<NavigationBarViewModel>();
		_presence = services.GetRequiredService<Presence>();
		BindingContext = _viewModel;
	}

	/// <summary>
	/// Loaded rather than the page's OnAppearing: a ContentView has no appearing of its own, and the
	/// unread badge is worth refreshing every time the bar comes back on screen.
	/// </summary>
	protected override void OnHandlerChanged()
	{
		base.OnHandlerChanged();

		if (Handler is null)
		{
			// The page is going away; stop the timer and let go of the presence subscription with it.
			_idleTimer?.Stop();
			_idleTimer = null;
			_viewModel.Dispose();
			return;
		}

		_viewModel.LoadCommand.Execute(null);
		StartWatchingForIdleness();
	}

	private void StartWatchingForIdleness()
	{
		_idleTimer = Dispatcher.CreateTimer();
		_idleTimer.Interval = IdleCheckInterval;
		_idleTimer.Tick += (_, _) => _presence.ReconsiderIdleness();
		_idleTimer.Start();
	}
}
