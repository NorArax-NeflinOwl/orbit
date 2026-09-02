using System.Windows.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Features.Chat;

public partial class ContactsPage : ContentPage
{
	private readonly ContactsViewModel _viewModel;
	private readonly Translations _translations;

	public ContactsPage(ContactsViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, for the reason TaskListDetailPage gives: the row template reads
		// the command once, as the tree is built, and one assigned afterwards is read as null.
		_viewModel = viewModel;
		_translations = translations;
		ShowContactMenuCommand = new Command<LocalContact>(contact => _ = ShowContactMenuAsync(contact));

		InitializeComponent();
		BindingContext = viewModel;
	}

	/// <summary>
	/// Typed so the row template's binding back up to the page can be compiled - see the comment in the
	/// XAML about why it goes through the page rather than naming the view model directly.
	/// </summary>
	public ContactsViewModel ViewModel => _viewModel;

	/// <summary>
	/// What a row's "⋯" opens: putting a conversation away, bringing it back, and emptying it - the
	/// three Orbit.Web keeps behind the same menu.
	/// </summary>
	public ICommand ShowContactMenuCommand { get; }

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	private async Task ShowContactMenuAsync(LocalContact? contact)
	{
		if (contact is null)
		{
			return;
		}

		var info = _translations["Info"];
		var putAway = contact.IsArchived ? _translations["Put back"] : _translations["Archive"];
		// Offered only where somebody has already decided they are done with the conversation, which is
		// the one place Orbit.Web offers it too.
		var clear = _translations["Delete chat history"];
		string[] choices = contact.IsArchived ? [info, putAway, clear] : [info, putAway];

		var chosen = await DisplayActionSheet(
			contact.DisplayName, _translations["Cancel"], destruction: null, choices);

		if (chosen == info)
		{
			_viewModel.OpenContactInfoCommand.Execute(contact.UserId);
			return;
		}

		if (chosen == putAway)
		{
			_viewModel.SetArchivedCommand.Execute(contact);
			return;
		}

		if (chosen != clear)
		{
			return;
		}

		// Asked before it happens: nothing on this phone or the server brings those words back.
		var confirmed = await DisplayAlert(
			_translations["Delete chat history"],
			_translations["Everything in this conversation goes, on your side only. This cannot be undone."],
			_translations["Delete"], _translations["Cancel"]);

		if (confirmed)
		{
			_viewModel.ClearHistoryCommand.Execute(contact);
		}
	}
}
