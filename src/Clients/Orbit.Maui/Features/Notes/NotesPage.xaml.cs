using System.Windows.Input;
using Orbit.Maui.Controls;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Notes;

namespace Orbit.Maui.Features.Notes;

public partial class NotesPage : ContentPage, ITitleMenu
{
	private readonly NotesViewModel _viewModel;
	private readonly Translations _translations;

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public NotesViewModel ViewModel => _viewModel;

	public NotesPage(NotesViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: the menu is bound from the static part of the tree,
		// which is built there and reads a page's plain property exactly once. See
		// CalendarEventDetailPage.
		_translations = translations;
		ShowTitleMenuCommand = new Command(ShowTheListMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
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
	}

	/// <summary>
	/// What hangs under the screen's name: how the list is read. Two entries that open the two sets
	/// rather than one panel holding both - a menu of eight choices under two silent headings is a
	/// menu nobody reads, and the design's own dropdown names the groups.
	/// </summary>
	private void ShowTheListMenu() => Menu.Show(
		[
			new ScreenMenuEntry(_translations["Sort"], ShowTheSortMenu),
			new ScreenMenuEntry(_translations["Filter"], ShowTheFilterMenu)
		]);

	/// <summary>
	/// What the list is ordered by, under the pins - which stay at the top whatever is chosen, so the
	/// heading says so rather than leaving the reader to notice.
	/// </summary>
	private void ShowTheSortMenu() => Menu.Show(
		ListMenus.SortOrders(_translations).Select(order => new ScreenMenuEntry(
			order.Name,
			() => Arrange(_viewModel.Arrangement with { SortOrder = order.Value }),
			order.Value == _viewModel.Arrangement.SortOrder)),
		_translations["Sort - pinned stay on top"]);

	private void ShowTheFilterMenu() => Menu.Show(
		ListMenus.Filters(_translations).Select(filter => new ScreenMenuEntry(
			filter.Name,
			() => Arrange(_viewModel.Arrangement with { Filter = filter.Value }),
			filter.Value == _viewModel.Arrangement.Filter)),
		_translations["Show"]);

	private void Arrange(ListArrangement arrangement) => _viewModel.ArrangeCommand.Execute(arrangement);
}
