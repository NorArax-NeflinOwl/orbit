using System.Windows.Input;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Features.Chat;

public partial class GroupConversationPage : ContentPage
{
	private readonly GroupConversationViewModel _viewModel;

	private readonly Translations _translations;

	public GroupConversationPage(GroupConversationViewModel viewModel)
	{
		_translations = IPlatformApplication.Current!.Services.GetRequiredService<Translations>();
		ShowMessageMenuCommand = new Command<ReadableChatMessage>(ShowMessageMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>
	/// What a message's "⋯" opens, exactly as in a one-to-one conversation. On the page rather than the
	/// view model because which actions a menu offers is what the screen shows rather than what the
	/// message knows - and keeping it here leaves the view model testable without one.
	/// </summary>
	public ICommand ShowMessageMenuCommand { get; }

	/// <summary>The panel it draws - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	private void ShowMessageMenu(ReadableChatMessage? message)
	{
		if (message?.GroupMessageId is null)
		{
			return;
		}

		// "Who has read this" is offered for anybody's message; editing and deleting only for the
		// reader's own - see ReadableChatMessage.CanBeChanged.
		List<ScreenMenuEntry> entries =
		[
			new(_translations["Who has read this"], () => _ = ShowReceiptsAsync(message))
		];

		if (message.CanBeChanged)
		{
			entries.Add(new ScreenMenuEntry(
				_translations["Edit"], () => _viewModel.StartEditingCommand.Execute(message)));
			entries.Add(new ScreenMenuEntry(
				_translations["Delete"], () => _viewModel.DeleteCommand.Execute(message)));
		}

		if (message.CanBeRepliedTo)
		{
			entries.Add(new ScreenMenuEntry(
				_translations["Reply"], () => _viewModel.StartReplyingCommand.Execute(message)));
		}

		Menu.Show(entries, _translations["Message options"], opensUpwards: true);
	}

	private async Task ShowReceiptsAsync(ReadableChatMessage message)
		=> await DisplayAlertAsync(
			_translations["Who has read this"],
			await _viewModel.DescribeReceiptsAsync(message),
			_translations["Close"]);

	/// <summary>Typed so the navigator can hand the page its group without casting the binding context.</summary>
	public GroupConversationViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
		_viewModel.StartPolling();
	}

	/// <summary>
	/// Polling belongs to the screen, not to the app - see GroupConversationViewModel for why this one
	/// ticks more slowly than a one-to-one conversation's.
	/// </summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.StopPolling();
	}
}
