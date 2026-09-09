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
	/// Takes the screen's name and its menu from the page the bar is sitting in.
	///
	/// Bound here rather than in the markup because the bar's own binding context is the shared view
	/// model, which knows nothing about which page it is on - and because the title has to keep
	/// following a page whose name changes as it is read (a conversation is named after whoever is in
	/// it, the calendar after the day being looked at), so it is a binding rather than a copied string.
	///
	/// A page that offers no menu of its own gets no chevron and no press - see ITitleMenu, which is
	/// what lets the screens be converted one at a time.
	/// </summary>
	private void FollowThePage()
	{
		if (PageAround(this) is not { } page)
		{
			return;
		}

		TitleLabel.SetBinding(Label.TextProperty, new Binding(nameof(Page.Title), source: page));

		if (page is ITitleSteps series)
		{
			Step(PreviousStep, PreviousPress, series.PreviousCommand, series.PreviousDescription);
			Step(NextStep, NextPress, series.NextCommand, series.NextDescription);
		}

		if (page is not ITitleMenu withMenu)
		{
			return;
		}

		TitleChevron.IsVisible = true;
		TitlePress.IsVisible = true;
		TitlePress.Command = withMenu.ShowTitleMenuCommand;
		SemanticProperties.SetDescription(TitlePress, page.Title ?? string.Empty);
	}

	/// <summary>
	/// One of the two arrows beside the name. Left out entirely where the page offers no command for
	/// it: an arrow that does nothing is worse than no arrow, and the centre of the bar is the width
	/// the name has to fit in.
	/// </summary>
	private static void Step(Grid host, Button press, System.Windows.Input.ICommand? command, string description)
	{
		if (command is null)
		{
			return;
		}

		host.IsVisible = true;
		press.Command = command;
		SemanticProperties.SetDescription(press, description);
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
			// The page is going away; the timer goes with it. The view model does not - it is shared with
			// the avatar's menu and outlives any one page.
			_idleTimer?.Stop();
			_idleTimer = null;
			return;
		}

		FollowThePage();
		_viewModel.LoadCommand.Execute(null);
		StartWatchingForIdleness();
	}

	/// <summary>
	/// The page this bar was placed on. Walked rather than asked for: a ContentView is handed no page,
	/// and the bar is pasted into the markup of every screen rather than wrapped around them.
	/// </summary>
	private static Page? PageAround(Element element)
	{
		for (var parent = element.Parent; parent is not null; parent = parent.Parent)
		{
			if (parent is Page page)
			{
				return page;
			}
		}

		return null;
	}

	private void StartWatchingForIdleness()
	{
		_idleTimer = Dispatcher.CreateTimer();
		_idleTimer.Interval = IdleCheckInterval;
		_idleTimer.Tick += (_, _) => _presence.ReconsiderIdleness();
		_idleTimer.Start();
	}
}
