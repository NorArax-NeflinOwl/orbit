using System.Windows.Input;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Features.Chat;

public partial class GroupConversationPage : ContentPage
{
	private readonly GroupConversationViewModel _viewModel;

	private readonly Translations _translations;

	public GroupConversationPage(GroupConversationViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_translations = IPlatformApplication.Current!.Services.GetRequiredService<Translations>();
		ShowMessageMenuCommand = new Command<ReadableChatMessage>(message => _ = ShowMessageMenuAsync(message));
	}

	/// <summary>
	/// What a message's "⋯" opens, exactly as in a one-to-one conversation. On the page rather than the
	/// view model because an action sheet is a page's own presentation - and keeping it here leaves the
	/// view model, and the commands it carries, testable without one.
	/// </summary>
	public ICommand ShowMessageMenuCommand { get; }

	private async Task ShowMessageMenuAsync(ReadableChatMessage? message)
	{
		if (message?.GroupMessageId is null)
		{
			return;
		}

		// "Who has read this" is offered for anybody's message; editing and deleting only for the
		// reader's own - see ReadableChatMessage.CanBeChanged.
		var actions = new List<string> { _translations["Who has read this"] };
		if (message.CanBeChanged)
		{
			actions.Add(_translations["Edit"]);
			actions.Add(_translations["Delete"]);
		}

		var chosen = await DisplayActionSheetAsync(
			_translations["Message options"], _translations["Cancel"], destruction: null, [.. actions]);

		if (chosen == _translations["Who has read this"])
		{
			await ShowReceiptsAsync(message);
		}
		else if (chosen == _translations["Edit"])
		{
			_viewModel.StartEditingCommand.Execute(message);
		}
		else if (chosen == _translations["Delete"])
		{
			_viewModel.DeleteCommand.Execute(message);
		}
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
