using System.Windows.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
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
		ShowGroupMenuCommand = new Command<LocalChatGroup>(group => _ = ShowGroupMenuAsync(group));

		InitializeComponent();
		BindingContext = viewModel;
	}

	/// <summary>What a row's "⋯" opens: putting a group away, bringing it back, and leaving it.</summary>
	public ICommand ShowGroupMenuCommand { get; }

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	private async Task ShowGroupMenuAsync(LocalChatGroup? group)
	{
		if (group is null)
		{
			return;
		}

		var putAway = group.IsArchived ? _translations["Put back"] : _translations["Archive"];
		var pin = group.IsPinned ? _translations["Unpin"] : _translations["Pin"];
		var leave = _translations["Leave group"];

		// Leaving is marked as the destructive one: putting a group away changes nothing for anybody
		// else, and leaving is seen by the whole group. Pinning is not offered on something put away -
		// see ContactsPage.
		string[] choices = group.IsArchived ? [putAway] : [pin, putAway];
		var chosen = await DisplayActionSheet(group.Name, _translations["Cancel"], leave, choices);

		if (chosen == pin)
		{
			_viewModel.TogglePinCommand.Execute(group);
			return;
		}

		if (chosen == putAway)
		{
			_viewModel.SetArchivedCommand.Execute(group);
			return;
		}

		if (chosen != leave)
		{
			return;
		}

		var confirmed = await DisplayAlert(
			_translations["Leave group"],
			_translations["You stop receiving what is posted, and the group sees you go."],
			_translations["Leave group"], _translations["Cancel"]);

		if (confirmed)
		{
			_viewModel.LeaveCommand.Execute(group);
		}
	}
}
