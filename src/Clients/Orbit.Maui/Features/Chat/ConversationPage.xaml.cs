using System.Windows.Input;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Features.Chat;

public partial class ConversationPage : ContentPage
{
	private readonly ConversationViewModel _viewModel;

	private readonly Translations _translations;

	public ConversationPage(ConversationViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_translations = IPlatformApplication.Current!.Services.GetRequiredService<Translations>();
		ShowMessageMenuCommand = new Command<ReadableChatMessage>(message => _ = ShowMessageMenuAsync(message));
	}

	/// <summary>
	/// What a message's "⋯" opens. Lives on the page rather than the view model because an action sheet
	/// is a page's own presentation - and keeping it here leaves the view model, and the commands it
	/// carries, testable without one.
	/// </summary>
	public ICommand ShowMessageMenuCommand { get; }

	private async Task ShowMessageMenuAsync(ReadableChatMessage? message)
	{
		if (message is null)
		{
			return;
		}

		var actions = new List<string>();
		if (message.CanBeChanged)
		{
			actions.Add(_translations["Edit"]);
			actions.Add(_translations["Delete"]);
		}

		if (message.CanBeForwarded)
		{
			actions.Add(_translations["Forward"]);
		}

		var chosen = await DisplayActionSheetAsync(
			_translations["Message options"], _translations["Cancel"], destruction: null, actions.ToArray());

		if (chosen == _translations["Edit"])
		{
			_viewModel.StartEditingCommand.Execute(message);
		}
		else if (chosen == _translations["Delete"])
		{
			_viewModel.DeleteCommand.Execute(message);
		}
		else if (chosen == _translations["Forward"])
		{
			_viewModel.StartForwardingCommand.Execute(message);
		}
	}

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
