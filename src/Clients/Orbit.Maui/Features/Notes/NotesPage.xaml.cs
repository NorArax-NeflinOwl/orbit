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
	/// What hangs under the screen's name: how the list is read, in one panel of two named groups.
	///
	/// It used to be two entries that opened the two sets in turn, on the reasoning that eight choices
	/// under two silent headings is a menu nobody reads. The headings are not silent any more - a menu
	/// is groups now, each under its own, which is what the design draws - so the second press is gone
	/// and both halves are on screen at once. The sort heading still says the pins stay on top
	/// whatever is chosen, rather than leaving the reader to notice.
	/// </summary>
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
