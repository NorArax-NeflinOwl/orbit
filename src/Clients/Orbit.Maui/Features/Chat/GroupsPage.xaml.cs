using System.Windows.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Features.Chat;

public partial class GroupsPage : ContentPage
{
	private readonly GroupsViewModel _viewModel;
	private readonly Translations _translations;

	public GroupsPage(GroupsViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent - see ContactsPage.
		_viewModel = viewModel;
		_translations = translations;
		ShowGroupMenuCommand = new Command<LocalChatGroup>(ShowGroupMenu);

		InitializeComponent();
		BindingContext = viewModel;
	}

	/// <summary>What a row's "⋯" opens: putting a group away, bringing it back, and leaving it.</summary>
	public ICommand ShowGroupMenuCommand { get; }

	/// <summary>The panel it draws - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	private void ShowGroupMenu(LocalChatGroup? group)
	{
		if (group is null)
		{
			return;
		}

		List<ScreenMenuEntry> entries = [];

		// Pinning is not offered on something put away - see ContactsPage.
		if (!group.IsArchived)
		{
			entries.Add(new ScreenMenuEntry(
				group.IsPinned ? _translations["Unpin"] : _translations["Pin"],
				() => _viewModel.TogglePinCommand.Execute(group)));
		}

		entries.Add(new ScreenMenuEntry(
			group.IsArchived ? _translations["Put back"] : _translations["Archive"],
			() => _viewModel.SetArchivedCommand.Execute(group)));

		// Last, because it is the one everybody else sees: putting a group away changes nothing for
		// anybody but the reader, and leaving is seen by the whole group.
		entries.Add(new ScreenMenuEntry(_translations["Leave group"], () => _ = LeaveAsync(group)));

		Menu.Show(entries, group.Name, opensUpwards: true);
	}

	/// <summary>Asked before it happens, because the whole group sees the answer.</summary>
	private async Task LeaveAsync(LocalChatGroup group)
	{
		var confirmed = await DisplayAlertAsync(
			_translations["Leave group"],
			_translations["You stop receiving what is posted, and the group sees you go."],
			_translations["Leave group"], _translations["Cancel"]);

		if (confirmed)
		{
			_viewModel.LeaveCommand.Execute(group);
		}
	}
}
