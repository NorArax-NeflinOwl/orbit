using System.Windows.Input;
using Orbit.Maui.Controls;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Notifications;

namespace Orbit.Maui.Features.Notifications;

public partial class NotificationFeedPage : ContentPage, ITitleMenu
{
	private readonly NotificationFeedViewModel _viewModel;
	private readonly Translations _translations;

	public NotificationFeedPage(NotificationFeedViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, which is where the bar reads it - see TasksPage for the same
		// order and why it matters.
		ShowTitleMenuCommand = new Command(ShowFeedMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_translations = translations;
	}

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public NotificationFeedViewModel ViewModel => _viewModel;

	/// <inheritdoc />
	public ICommand ShowTitleMenuCommand { get; }

	/// <inheritdoc />
	public ScreenMenu Menu { get; } = new();

	/// <summary>
	/// What is done to the feed rather than to anything in it. These were three link buttons in a row
	/// above the list, which is where a reader looks for the newest thing - and two of them are refused
	/// with no connection, which a menu can say by greying an entry and a row of buttons could not.
	/// </summary>
	private void ShowFeedMenu() => Menu.Show(
	[
		new ScreenMenuEntry(
			_translations["Mark all read"],
			() => _viewModel.MarkEverythingReadCommand.Execute(null),
			canBeChosen: _viewModel.Connection.IsMet),
		// "Delete history" rather than "Clear", because it deletes: the browser's own button was renamed
		// on 2026-09-14 for that reason and this one was missed. Dismissing what is on screen and
		// throwing the feed away are different things, and only one of them can be undone by waiting.
		new ScreenMenuEntry(
			_translations["Delete history"],
			() => _viewModel.ClearCommand.Execute(null),
			canBeChosen: _viewModel.Connection.IsMet),
		new ScreenMenuEntry(
			_viewModel.ShowEverythingLabel,
			() => _viewModel.ShowEverythingCommand.Execute(null))
	],
	// And says why, when two of the three are greyed. A greyed entry with nothing beside it is a press
	// that did not register as far as the reader is concerned - which is how "clearing the feed does
	// nothing" (2026-09-18, issue #296) reads from the outside whether or not anything is wrong. The
	// wording is ConnectionRequirement's own, so it says here what it says on the share panel and on a
	// task list.
	heading: _viewModel.Connection.IsNotMet ? _viewModel.Connection.Explanation : null);

	/// <summary>
	/// Reloaded every time rather than once: the feed's whole subject is what happened while the reader
	/// was somewhere else, so coming back to it is exactly when it is most likely to be out of date.
	/// </summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}
