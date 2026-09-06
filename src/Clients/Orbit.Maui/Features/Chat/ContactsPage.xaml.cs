using System.Windows.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
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
		ShowContactMenuCommand = new Command<LocalContact>(ShowContactMenu);

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

	/// <summary>The panel it draws - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	private void ShowContactMenu(LocalContact? contact)
	{
		if (contact is null)
		{
			return;
		}

		List<ScreenMenuEntry> entries =
		[
			new(_translations["Info"], () => _viewModel.OpenContactInfoCommand.Execute(contact.UserId))
		];

		// Not offered on something put away: pinning keeps a conversation at the top of the day, which
		// is the opposite of what archiving said - see ContactsViewModel.InReadingOrder.
		if (!contact.IsArchived)
		{
			entries.Add(new ScreenMenuEntry(
				contact.IsPinned ? _translations["Unpin"] : _translations["Pin"],
				() => _viewModel.TogglePinCommand.Execute(contact)));
		}

		entries.Add(new ScreenMenuEntry(
			contact.IsArchived ? _translations["Put back"] : _translations["Archive"],
			() => _viewModel.SetArchivedCommand.Execute(contact)));

		// Offered only where somebody has already decided they are done with the conversation, which is
		// the one place Orbit.Web offers it too.
		if (contact.IsArchived)
		{
			entries.Add(new ScreenMenuEntry(
				_translations["Delete chat history"], () => _ = ClearHistoryAsync(contact)));
		}

		Menu.Show(entries, contact.DisplayName, opensUpwards: true);
	}

	/// <summary>Asked before it happens: nothing on this phone or the server brings those words back.</summary>
	private async Task ClearHistoryAsync(LocalContact contact)
	{
		var confirmed = await DisplayAlertAsync(
			_translations["Delete chat history"],
			_translations["Everything in this conversation goes, on your side only. This cannot be undone."],
			_translations["Delete"], _translations["Cancel"]);

		if (confirmed)
		{
			_viewModel.ClearHistoryCommand.Execute(contact);
		}
	}
}
