using System.Windows.Input;
using Orbit.Maui.Controls;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Places;

namespace Orbit.Maui.Features.Places;

/// <summary>
/// One place: a name, a point and three answers about how it is drawn. The whole of a place, which is
/// why there is nothing behind this screen - and everything that can be *done* to it hangs under its
/// name in the bar, the way it does on every other screen that is about one thing.
/// </summary>
public partial class PlaceDetailPage : ContentPage, ITitleMenu
{
	private readonly Translations _translations;

	public PlaceDetailPage(PlaceDetailViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, for the reason NotesPage gives.
		_translations = translations;
		ShowTitleMenuCommand = new Command(ShowThePlaceMenu);

		InitializeComponent();
		BindingContext = ViewModel = viewModel;
	}

	public PlaceDetailViewModel ViewModel { get; }

	/// <inheritdoc cref="ITitleMenu.ShowTitleMenuCommand"/>
	public ICommand ShowTitleMenuCommand { get; }

	/// <summary>The panel it draws - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	/// <summary>
	/// What can be done to this place. Sharing opens the panel in place rather than a screen of its own;
	/// deleting is named for what it will actually do, because a place somebody handed over is not this
	/// reader's to destroy - the same press takes it off their own map and leaves its owner's alone.
	/// </summary>
	private void ShowThePlaceMenu()
	{
		List<ScreenMenuEntry> entries =
		[
			new ScreenMenuEntry(
				_translations["Share"],
				() => Sharing.IsVisible = !Sharing.IsVisible,
				Sharing.IsVisible)
		];

		// The other way off the map, and immediately above Delete on purpose: somebody reaching for
		// Delete because they want this out of the way should meet it first - one of the two is
		// reversible. Only for this reader's own: putting away a place somebody handed over would take
		// it off the map of the person who keeps it.
		if (!ViewModel.IsSharedWithMe)
		{
			entries.Add(new ScreenMenuEntry(
				ViewModel.IsArchived ? _translations["Put back"] : _translations["Archive"],
				() => ViewModel.ArchiveCommand.Execute(!ViewModel.IsArchived)));
		}

		// Deleting a place is offered in the archive and nowhere else (the user's rule, 2026-09-19) -
		// so a place in use offers Archive above and no Delete at all. Taking somebody else's off this
		// map is not a deletion and needs no archive: the owner keeps it, and there is nothing to put
		// away first.
		if (ViewModel.IsSharedWithMe || ViewModel.IsArchived)
		{
			entries.Add(new ScreenMenuEntry(
				ViewModel.IsSharedWithMe ? _translations["Take it off my map"] : _translations["Delete"],
				() => ViewModel.DeleteCommand.Execute(null)));
		}

		Menu.Show(entries);
	}
}
