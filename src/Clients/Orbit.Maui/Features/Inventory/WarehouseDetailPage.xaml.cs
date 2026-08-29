using Orbit.Mobile.Localization;
using System.Windows.Input;
using Orbit.Mobile.Screens.Inventory;

namespace Orbit.Maui.Features.Inventory;

public partial class WarehouseDetailPage : ContentPage
{
	private readonly WarehouseDetailViewModel _viewModel;
	private readonly Translations _translations;

	public WarehouseDetailPage(WarehouseDetailViewModel viewModel, Translations translations)
	{
		InitializeComponent();
		_translations = translations;
		_viewModel = viewModel;
		BindingContext = viewModel;
		ShowItemMenuCommand = new Command<WarehouseItemRow>(item => _ = ShowItemMenuAsync(item));
	}

	/// <summary>
	/// Typed so the item template's bindings back up to the page can be compiled - see the comment in the
	/// XAML about why they go through the page rather than naming the view model directly.
	/// </summary>
	public WarehouseDetailViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
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

	private async Task ShowItemMenuAsync(WarehouseItemRow? item)
	{
		if (item is null)
		{
			return;
		}

		var remove = _translations["Delete item"];
		var moveUp = _translations["Move up"];
		var moveDown = _translations["Move down"];
		var chosen = await DisplayActionSheet(
			_translations["Item options"], _translations["Cancel"], remove,
			_translations["Edit"], moveUp, moveDown);

		if (chosen == remove)
		{
			_viewModel.RemoveItemCommand.Execute(item);
		}
		else if (chosen == _translations["Edit"])
		{
			_viewModel.EditItemCommand.Execute(item);
		}
		else if (chosen == moveUp)
		{
			_viewModel.MoveItemUpCommand.Execute(item);
		}
		else if (chosen == moveDown)
		{
			_viewModel.MoveItemDownCommand.Execute(item);
		}
	}
}
