using System.Windows.Input;
using Orbit.Maui.Controls;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Places;

namespace Orbit.Maui.Features.Places;

public partial class PlacesPage : ContentPage, ITitleMenu
{
	private readonly PlacesViewModel _viewModel;

	/// <summary>Redraws this screen when a sync it did not ask for brings something - see ScreenKeptInStep.</summary>
	private readonly ScreenKeptInStep _keptInStep;
	private readonly Translations _translations;

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public PlacesViewModel ViewModel => _viewModel;

	public PlacesPage(PlacesViewModel viewModel, Translations translations, SyncState syncState)
	{
		// Before InitializeComponent, for the reason NotesPage gives.
		_translations = translations;
		ShowTitleMenuCommand = new Command(ShowTheListMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_keptInStep = new ScreenKeptInStep(syncState, () => _viewModel.ShowLocalPlacesAsync(CancellationToken.None));
		AddButton.Command = NewItemForm.Toggling(AddRow, AddField);
	}

	/// <inheritdoc cref="ITitleMenu.ShowTitleMenuCommand"/>
	public ICommand ShowTitleMenuCommand { get; }

	/// <summary>The panel it draws - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
		_keptInStep.Listen();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_keptInStep.StopListening();
	}

	/// <inheritdoc cref="Notes.NotesPage.ShowTheListMenu"/>
	private void ShowTheListMenu() => Menu.ShowGroups(
		[
			new ScreenMenuGroup(
				_translations["Sort - pinned stay on top"],
				ListMenus.SortOrders(_translations).Select(order => new ScreenMenuEntry(
					order.Name,
					() => Arrange(_viewModel.Arrangement with { SortOrder = order.Value }),
					order.Value == _viewModel.Arrangement.SortOrder))),
			new ScreenMenuGroup(
				_translations["Show"],
				ListMenus.Filters(_translations).Select(filter => new ScreenMenuEntry(
					filter.Name,
					() => Arrange(_viewModel.Arrangement with { Filter = filter.Value }),
					filter.Value == _viewModel.Arrangement.Filter)))
		]);

	private void Arrange(ListArrangement arrangement) => _viewModel.ArrangeCommand.Execute(arrangement);
}
