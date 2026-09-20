using System.Windows.Input;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Features.Chat;

public partial class ConversationPage : ContentPage
{
	private readonly ConversationViewModel _viewModel;

	private readonly Translations _translations;

	public ConversationPage(ConversationViewModel viewModel)
	{
		_translations = IPlatformApplication.Current!.Services.GetRequiredService<Translations>();

		// Before InitializeComponent, not after: the thread's own menu is bound from the static part of
		// the tree, which reads a page's plain property exactly once - see CalendarEventDetailPage.
		ShowThreadMenuCommand = new Command(ShowThreadMenu);
		ShowMessageMenuCommand = new Command<ReadableChatMessage>(ShowMessageMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>
	/// What a message's "⋯" opens. Lives on the page rather than the view model because which actions
	/// a menu offers is what the screen shows rather than what the message knows - and keeping it here
	/// leaves the view model, and the commands it carries, testable without one.
	/// </summary>
	public ICommand ShowMessageMenuCommand { get; }

	/// <summary>And the conversation's own, in the corner of the thread header.</summary>
	public ICommand ShowThreadMenuCommand { get; }

	/// <summary>The panel both of them draw - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	/// <summary>
	/// Everything a message can have done to it, in Orbit's own panel rather than the platform's action
	/// sheet - the same four the browser hangs off a bubble, each left out where it does not apply.
	/// </summary>
	private void ShowMessageMenu(ReadableChatMessage? message)
	{
		if (message is null)
		{
			return;
		}

		// What the bubble no longer says by itself: when it was sent, and how far it got. The thread
		// shows the time under the newest message alone now - see ReadableChatMessage.IsTheNewest - so
		// this is where every other message answers "when was that". First, as the group thread's own
		// "who has read this" is.
		List<ScreenMenuEntry> entries =
		[
			new(_translations["Info"], () => _ = ShowMessageInfoAsync(message))
		];

		if (message.CanBeChanged)
		{
			entries.Add(new ScreenMenuEntry(
				_translations["Edit"], () => _viewModel.StartEditingCommand.Execute(message)));
			entries.Add(new ScreenMenuEntry(
				_translations["Delete"], () => _viewModel.DeleteCommand.Execute(message)));
		}

		if (message.CanBeForwarded)
		{
			// "Forward" on its own said nothing to the person who asked for this (2026-09-20): it names
			// a mechanism rather than what pressing it does, and what it does is pick somebody to send
			// this same message on to.
			entries.Add(new ScreenMenuEntry(
				_translations["Pass on to somebody else"], () => _viewModel.StartForwardingCommand.Execute(message)));
		}

		if (message.CanBeRepliedTo)
		{
			entries.Add(new ScreenMenuEntry(
				_translations["Reply"], () => _viewModel.StartReplyingCommand.Execute(message)));
		}

		Menu.Show(entries, _translations["Message options"], placement: MenuPlacement.FromTheFoot);
	}

	/// <summary>
	/// What this one message says about itself - see ConversationViewModel.DescribeMessage, which writes
	/// it. In the platform's own dialog, as the group thread's receipts are: it is read and dismissed.
	/// </summary>
	private async Task ShowMessageInfoAsync(ReadableChatMessage message)
		=> await DisplayAlertAsync(
			_translations["Info"], _viewModel.DescribeMessage(message), _translations["Close"]);

	/// <summary>
	/// Who this is, apart from what they have said. One entry today, and it is the one Orbit.Web's own
	/// thread menu opens with.
	/// </summary>
	private void ShowThreadMenu() => Menu.Show(
		[new ScreenMenuEntry(_translations["Info"], () => _viewModel.OpenContactInfoCommand.Execute(null))]);

	/// <summary>Typed so the navigator can hand the page its contact without casting the binding context.</summary>
	public ConversationViewModel ViewModel => _viewModel;

	/// <summary>
	/// The window this page is showing in, held for as long as it is showing so its Stopped and Resumed
	/// can be let go of again - see OnDisappearing.
	/// </summary>
	private Window? _window;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
		_viewModel.StartPolling();

		// Going to the background does not make a page disappear, so the window is what says it.
		_window = Window;
		if (_window is not null)
		{
			_window.Stopped += OnAppStopped;
			_window.Resumed += OnAppResumed;
		}

		_ = _viewModel.ScreenShownAsync();
	}

	/// <summary>
	/// Polling belongs to the screen, not to the app - a conversation nobody is looking at should cost
	/// nothing. See ConversationViewModel for why this is not the web client's once-a-second loop.
	/// </summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.ScreenHidden();
		if (_window is not null)
		{
			_window.Stopped -= OnAppStopped;
			_window.Resumed -= OnAppResumed;
			_window = null;
		}

		_viewModel.StopPolling();
	}

	private void OnAppStopped(object? sender, EventArgs e) => _viewModel.AppWentToBackground();

	private void OnAppResumed(object? sender, EventArgs e) => _ = _viewModel.AppCameToForegroundAsync();

	/// <summary>
	/// The last line on screen, which is what "read" is measured by - see ConversationReadState. Raised
	/// as the reader scrolls and as new lines are laid out at the bottom.
	/// </summary>
	private void OnThreadScrolled(object? sender, ItemsViewScrolledEventArgs e)
		=> _ = _viewModel.ShowedUpToAsync(e.LastVisibleItemIndex);

	/// <summary>
	/// Brings the newest message back into view when the keyboard opens.
	///
	/// The keyboard takes the bottom of the screen and the thread is given the room that is left - see
	/// MainActivity's insets listener - but a list keeps the offset it was scrolled to rather than the
	/// item it was showing, so the end of the conversation ends up behind the keyboard. Somebody who
	/// tapped the box to answer the message they were reading had to scroll to find it again (reported
	/// 2026-09-20). ItemsUpdatingScrollMode does not cover this: nothing was added, the view was
	/// resized.
	///
	/// After the resize rather than with it - the room is not taken until the keyboard is actually up,
	/// and scrolling before that scrolls to where the end used to be.
	/// </summary>
	private void OnComposeFocused(object? sender, FocusEventArgs e)
		=> Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), ShowTheNewestMessage);

	private void ShowTheNewestMessage()
	{
		if (_viewModel.Messages.Count == 0)
		{
			return;
		}

		Thread.ScrollTo(_viewModel.Messages[^1], position: ScrollToPosition.End, animate: false);
	}
}
