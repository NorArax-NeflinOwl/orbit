using System.Windows.Input;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Dashboard;

namespace Orbit.Maui.Features.Dashboard;

public partial class DashboardPage : ContentPage
{
	private readonly DashboardViewModel _viewModel;
	private readonly Translations _translations;

	public DashboardPage(DashboardViewModel viewModel, Translations translations)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_translations = translations;
		ShowCardFilterCommand = new Command<DashboardCard>(card => _ = ShowCardFilterAsync(card));
	}

	/// <summary>Typed so the card rows' bindings back up to the page can be compiled.</summary>
	public DashboardViewModel ViewModel => _viewModel;

	/// <summary>
	/// What a card's "⋯" opens. On the page rather than the view model because an action sheet is a
	/// page's own presentation - the same split as the note editor's line menu.
	/// </summary>
	public ICommand ShowCardFilterCommand { get; }

	/// <summary>
	/// Reloaded every time. This reads the local store, which every synchroniser writes to behind the
	/// app's back, so coming back to the dashboard is exactly when it is most likely to be stale.
	/// </summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	private async Task ShowCardFilterAsync(DashboardCard? card)
	{
		if (card is null || _viewModel.FilterChoicesFor(card.Kind) is not { Count: > 0 } choices)
		{
			return;
		}

		// The one in force is marked, because a menu of four with no answer among them leaves the
		// reader guessing what the card is currently showing.
		var names = choices
			.Select(choice => choice.IsChosen ? $"{choice.Name} ✓" : choice.Name)
			.ToArray();

		var chosen = await DisplayActionSheet(card.Title, _translations["Cancel"], destruction: null, names);
		if (Array.IndexOf(names, chosen) is var picked and >= 0)
		{
			await _viewModel.ChooseFilterCommand.ExecuteAsync(choices[picked]);
		}
	}
}
