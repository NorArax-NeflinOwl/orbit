using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Orbit.Maui.Platform;
using Orbit.Mobile.Localization;
using Orbit.Maui.Controls;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Tasks;
using SensorLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace Orbit.Maui.Features.Tasks;

public partial class TaskItemSummaryPage : ContentPage, ITitleMenu
{
	/// <summary>
	/// How much ground the map shows. Closer in than the positions map: this is one place somebody has
	/// to get to, so the street it is on matters more than the neighbourhood around it.
	/// </summary>
	private static readonly Distance InitialRadius = Distance.FromKilometers(1);

	private readonly TaskItemSummaryViewModel _viewModel;
	private readonly Translations _translations;

	/// <summary>False when this build cannot show one, which makes the pin below pointless.</summary>
	private bool _hasMap = true;

	public TaskItemSummaryPage(TaskItemSummaryViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: the rail's menu is bound from the static part of the
		// tree, which reads a page's plain property exactly once - see CalendarEventDetailPage.
		_translations = translations;
		ShowTitleMenuCommand = new Command(ShowEntryMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;

		if (!MapAvailability.CanShowMap)
		{
			SayThereIsNoMap(translations);
		}
	}

	/// <summary>What the rail's three dots open.</summary>
	public ICommand ShowTitleMenuCommand { get; }

	/// <summary>The panel they draw - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	/// <summary>
	/// The other place this entry can be met, and the two things the design's menu does to the entry
	/// itself - a copy of it, and taking it off its list. Orbit.Web's own entry page offers the same
	/// since 2026-09-11. Moving it to another list is not here: that needs a list picker the design
	/// does not draw.
	/// </summary>
	private void ShowEntryMenu() => Menu.Show(
	[
		new ScreenMenuEntry(_translations["Show Tasks"], () => _viewModel.ShowTaskListCommand.Execute(null)),
		new ScreenMenuEntry(_translations["Duplicate"], () => _viewModel.DuplicateCommand.Execute(null)),
		new ScreenMenuEntry(_translations["Delete item"], () => _ = DeleteAfterAskingAsync())
	]);

	/// <summary>
	/// Asked first, as every irreversible press is - see Confirmation - and in the words Orbit.Web's
	/// entry page asks it in.
	/// </summary>
	private async Task DeleteAfterAskingAsync()
	{
		if (await Confirmation.AskAsync(
				this, _translations.Format("Delete \"{0}\"?", _viewModel.Description),
				_translations["Delete"], _translations["Cancel"]))
		{
			await _viewModel.DeleteCommand.ExecuteAsync(null);
		}
	}

	/// <summary>
	/// Takes the map out before anything renders it, for the reason MapPage gives: on Android a map
	/// built without a key throws from inside Play Services and ends the process. What this screen is
	/// for - what the entry is, when it is, and the address in words - does not need the picture.
	/// </summary>
	private void SayThereIsNoMap(Translations translations)
	{
		_hasMap = false;
		MapArea.Content = new Label
		{
			Text = translations["The map can't be shown in this build."],
			FontSize = 12,
			VerticalOptions = LayoutOptions.Center,
			HorizontalTextAlignment = TextAlignment.Center,
			LineBreakMode = LineBreakMode.WordWrap
		};
	}

	public TaskItemSummaryViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.PropertyChanged += OnViewModelPropertyChanged;
		_viewModel.LoadCommand.Execute(null);
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.PropertyChanged -= OnViewModelPropertyChanged;
	}

	/// <summary>
	/// The pin arrives after the screen does - an address of the entry's own has to be looked up first -
	/// so the map is drawn when the view model says where it goes rather than when the page appears.
	/// </summary>
	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
	{
		if (eventArgs.PropertyName == nameof(TaskItemSummaryViewModel.Pin))
		{
			ShowThePin();
		}
	}

	private void ShowThePin()
	{
		if (!_hasMap)
		{
			return;
		}

		PlaceMap.Pins.Clear();
		if (_viewModel.Pin is not { } point)
		{
			return;
		}

		var where = new SensorLocation(point.Latitude, point.Longitude);
		PlaceMap.Pins.Add(new Pin { Label = point.Label, Address = point.Description, Location = where });

		// Moved on the next turn of the loop, not now: the pin arriving is also what makes the map
		// visible - MapArea is bound to HasPin - so at this moment the map has not been laid out yet and
		// Google's own view drops the move without a word. The screen was left showing the whole
		// Atlantic with the pin somewhere off it, which reads as a map that failed to find the place
		// rather than one that was told where to go too early.
		Dispatcher.Dispatch(() => PlaceMap.MoveToRegion(MapSpan.FromCenterAndRadius(where, InitialRadius)));
	}
}
