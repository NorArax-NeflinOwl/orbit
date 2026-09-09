using System.Windows.Input;
using Orbit.Mobile.Localization;
using Orbit.Maui.Controls;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Dashboard;

namespace Orbit.Maui.Features.Dashboard;

public partial class DashboardPage : ContentPage, ITitleMenu
{
	private readonly DashboardViewModel _viewModel;
	private readonly Translations _translations;

	public DashboardPage(DashboardViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: both are bound from the static part of the tree, which
		// is built there and reads a page's plain property exactly once - see CalendarEventDetailPage,
		// where the same order matters for the same reason.
		_translations = translations;
		ShowTitleMenuCommand = new Command(ShowTheDashboardMenu);
		ShowCardFilterCommand = new Command<DashboardCard>(ShowCardFilter);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Typed so the card rows' bindings back up to the page can be compiled.</summary>
	public DashboardViewModel ViewModel => _viewModel;

	/// <summary>What the three dots at the header's other end open - which parts of the page are wanted.</summary>
	public ICommand ShowTitleMenuCommand { get; }

	/// <summary>And what a card's own three dots open: what that card is narrowed to.</summary>
	public ICommand ShowCardFilterCommand { get; }

	/// <summary>The panel both of them draw - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	/// <summary>
	/// Reloaded every time. This reads the local store, which every synchroniser writes to behind the
	/// app's back, so coming back to the dashboard is exactly when it is most likely to be stale.
	/// </summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	/// <summary>
	/// What hangs under the screen's name: what is on the page, and what order it is in - one panel of
	/// two named groups, which is what the design draws.
	///
	/// It used to be two entries that opened the two sets in turn, on the reasoning that the parts list
	/// is every card Orbit has and a menu that long with an order at the top of it is a menu nobody
	/// reads. A menu is groups now, each under its own heading, so the length is legible and both halves
	/// are on screen at once.
	///
	/// Rebuilt from scratch on every choice that leaves it open, rather than ticking the entry that was
	/// pressed: putting a part away rebuilds the choices, so the entries this menu is holding are no
	/// longer the ones that know their own answer.
	/// </summary>
	private void ShowTheDashboardMenu() => Menu.ShowGroups(
		[
			// Which parts of the dashboard are wanted at all. Settings rather than actions, so they stay
			// open while several are changed - the exception Orbit.Web's OverflowMenu.StaysOpen makes
			// for exactly this menu.
			new ScreenMenuGroup(
				_translations["Show on the dashboard"],
				_viewModel.CardChoices.Select(choice => new ScreenMenuEntry(
					choice.Name,
					() =>
					{
						_viewModel.ToggleCardShownCommand.Execute(choice);
						ShowTheDashboardMenu();
					},
					choice.IsShown,
					staysOpen: true))),

			// What order the cards are in under the pins - which stay at the top whatever is chosen, so
			// the heading says so rather than leaving the reader to notice.
			new ScreenMenuGroup(
				_translations["Sort - pinned stay on top"],
				[
					new(_translations["Orbit's order"],
						() => _viewModel.ArrangeCommand.Execute(DashboardCardOrder.Standard),
						_viewModel.Order is DashboardCardOrder.Standard),
					new(_translations["Name"],
						() => _viewModel.ArrangeCommand.Execute(DashboardCardOrder.Name),
						_viewModel.Order is DashboardCardOrder.Name)
				])
		]);

	/// <summary>
	/// What one card is showing of what it could show - Orbit.Web's CardFilterMenu, under the same
	/// heading. One choice and then done, unlike the page's menu above.
	/// </summary>
	private void ShowCardFilter(DashboardCard? card)
	{
		if (card is null || _viewModel.FilterChoicesFor(card.Kind) is not { Count: > 0 } choices)
		{
			return;
		}

		// The one in force is marked, because a menu of four with no answer among them leaves the
		// reader guessing what the card is currently showing.
		Menu.Show(
			choices.Select(choice => new ScreenMenuEntry(
				choice.Name,
				() => _ = _viewModel.ChooseFilterCommand.ExecuteAsync(choice),
				choice.IsChosen)),
			_translations["Show"],
			placement: MenuPlacement.FromTheFoot);
	}
}
