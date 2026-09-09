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
		new ScreenMenuEntry(
			_translations["Clear"],
			() => _viewModel.ClearCommand.Execute(null),
			canBeChosen: _viewModel.Connection.IsMet),
		new ScreenMenuEntry(
			_viewModel.ShowEverythingLabel,
			() => _viewModel.ShowEverythingCommand.Execute(null))
	]);

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
