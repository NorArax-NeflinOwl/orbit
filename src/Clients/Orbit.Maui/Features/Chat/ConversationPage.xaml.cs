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

		List<ScreenMenuEntry> entries = [];

		if (message.CanBeChanged)
		{
			entries.Add(new ScreenMenuEntry(
				_translations["Edit"], () => _viewModel.StartEditingCommand.Execute(message)));
			entries.Add(new ScreenMenuEntry(
				_translations["Delete"], () => _viewModel.DeleteCommand.Execute(message)));
		}

		if (message.CanBeForwarded)
		{
			entries.Add(new ScreenMenuEntry(
				_translations["Forward"], () => _viewModel.StartForwardingCommand.Execute(message)));
		}

		if (message.CanBeRepliedTo)
		{
			entries.Add(new ScreenMenuEntry(
				_translations["Reply"], () => _viewModel.StartReplyingCommand.Execute(message)));
		}

		Menu.Show(entries, _translations["Message options"], opensUpwards: true);
	}

	/// <summary>
	/// Who this is, apart from what they have said. One entry today, and it is the one Orbit.Web's own
	/// thread menu opens with.
	/// </summary>
	private void ShowThreadMenu() => Menu.Show(
		[new ScreenMenuEntry(_translations["Info"], () => _viewModel.OpenContactInfoCommand.Execute(null))]);

	/// <summary>Typed so the navigator can hand the page its contact without casting the binding context.</summary>
	public ConversationViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
		_viewModel.StartPolling();
	}

	/// <summary>
	/// Polling belongs to the screen, not to the app - a conversation nobody is looking at should cost
	/// nothing. See ConversationViewModel for why this is not the web client's once-a-second loop.
	/// </summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.StopPolling();
	}
}
