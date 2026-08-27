using Orbit.Mobile.Localization;
using System.Windows.Input;
using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Features.Tasks;

public partial class TaskListDetailPage : ContentPage
{
	/// <summary>
	/// Typed so the item template's bindings back up to the page can be compiled - see the comment in
	/// the XAML about why they go through the page rather than naming the view model directly.
	/// </summary>
	public TaskListDetailViewModel ViewModel => _viewModel;

	private readonly TaskListDetailViewModel _viewModel;
	private readonly Translations _translations;

	public TaskListDetailPage(TaskListDetailViewModel viewModel, Translations translations)
	{
		InitializeComponent();
		_translations = translations;
		_viewModel = viewModel;
		BindingContext = viewModel;
		ShowItemMenuCommand = new Command<TaskItemRow>(item => _ = ShowItemMenuAsync(item));
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	/// <summary>
	/// Choosing a list moves the entry, which closes the editor the picker lives in - and iOS leaves its
	/// wheel on screen when the view under it disappears. Dismissing it first is the view's own business.
	/// </summary>
	private void OnMoveTargetChosen(object? sender, EventArgs eventArgs)
	{
		(sender as Picker)?.Unfocus();
	}

	/// <summary>Lets go of the edit lock as the screen leaves - see EditLock.</summary>
	protected override async void OnDisappearing()
	{
		base.OnDisappearing();
		await _viewModel.CloseAsync();
	}

	/// <summary>
	/// What a row's "⋯" opens. On the page rather than the view model because an action sheet is a
	/// page's own presentation - the same reason ConversationPage keeps its message menu here.
	/// </summary>
	public ICommand ShowItemMenuCommand { get; }

	private async Task ShowItemMenuAsync(TaskItemRow? item)
	{
		if (item is null)
		{
			return;
		}

		var remove = _translations["Delete item"];
		var chosen = await DisplayActionSheet(
			_translations["Item options"], _translations["Cancel"], remove, _translations["Edit"]);

		if (chosen == remove)
		{
			_viewModel.RemoveItemCommand.Execute(item);
		}
		else if (chosen == _translations["Edit"])
		{
			_viewModel.EditItemCommand.Execute(item);
		}
	}
}
