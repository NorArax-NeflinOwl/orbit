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
	private void ShowThePlaceMenu() => Menu.Show(
	[
		new ScreenMenuEntry(
			_translations["Share"],
			() => Sharing.IsVisible = !Sharing.IsVisible,
			Sharing.IsVisible),
		new ScreenMenuEntry(
			ViewModel.IsSharedWithMe ? _translations["Take it off my map"] : _translations["Delete"],
			() => ViewModel.DeleteCommand.Execute(null))
	]);
}
