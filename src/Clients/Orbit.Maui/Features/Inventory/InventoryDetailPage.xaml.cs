using Orbit.Mobile.Localization;
using System.Windows.Input;
using Orbit.Maui.Controls;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Inventory;

namespace Orbit.Maui.Features.Inventory;

public partial class InventoryDetailPage : ContentPage, ITitleMenu
{
	private readonly InventoryDetailViewModel _viewModel;
	private readonly Translations _translations;

	public InventoryDetailPage(InventoryDetailViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: the menu is bound from the static part of the tree,
		// which is built there and reads a page's plain property exactly once - see CalendarEventDetailPage.
		_translations = translations;
		ShowTitleMenuCommand = new Command(ShowShelfMenu);

		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;
		ShowItemMenuCommand = new Command<InventoryItemRow>(item => _ = ShowItemMenuAsync(item));
		AddButton.Command = NewItemForm.Toggling(AddRow, AddField);
	}

	/// <inheritdoc cref="ITitleMenu.ShowTitleMenuCommand"/>
	public ICommand ShowTitleMenuCommand { get; }

	/// <summary>The panel it draws - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	/// <summary>
	/// What can be done to the shelf rather than to what is on it. Its name, what it is, whether it is
	/// private and how its restock list behaves all stood above and below the shelf itself; the screen
	/// is the shelf now, and this is where they went.
	/// </summary>
	private void ShowShelfMenu()
	{
		List<ScreenMenuEntry> entries = [];

		if (_viewModel.CanEdit)
		{
			entries.Add(new ScreenMenuEntry(
				_translations["Edit"],
				() => ShelfFields.IsVisible = ShelfSettings.IsVisible = ShelfExtras.IsVisible = !ShelfSettings.IsVisible,
				ShelfSettings.IsVisible));
		}

		entries.Add(new ScreenMenuEntry(
			_translations["Share"],
			() => Sharing.IsVisible = !Sharing.IsVisible,
			Sharing.IsVisible,
			canBeChosen: !_viewModel.IsPrivate));

		// Which smaller shelves this one gathers - see Orbit.Core.Inventories.Inventory.GathersInventoryIds.
		// Greyed rather than hidden where it cannot be done, as everything needing a connection is: the
		// reader learns the option exists and what it is waiting for.
		entries.Add(new ScreenMenuEntry(
			_translations["Inventories gathered here"],
			() => _viewModel.ArrangeTheGroupCommand.Execute(null),
			canBeChosen: _viewModel.CanArrangeTheGroup));

		// The other way out of a list, and immediately above Delete on purpose: somebody reaching for
		// Delete because they want this out of the way should meet it first - one of the two is
		// reversible. Only for this reader's own, the way filing is - see BuiltInFolder.Archived.
		if (_viewModel.CanEdit)
		{
			entries.Add(new ScreenMenuEntry(
				_viewModel.IsArchived ? _translations["Put back"] : _translations["Archive"],
				() => _viewModel.ArchiveCommand.Execute(!_viewModel.IsArchived)));

			entries.Add(new ScreenMenuEntry(
				_translations["Delete inventory"], () => _viewModel.DeleteCommand.Execute(null)));
		}

		// Where this thing's own copies are found again - see CopyHistoryViewModel. Only once there is
		// one, and here rather than in the account's menu: a history belongs to the thing it is the
		// history of.
		if (_viewModel.HasHistory)
		{
			entries.Add(new ScreenMenuEntry(
				_translations["History"], () => _viewModel.GoToHistoryCommand.Execute(null)));
		}

		// Where it is filed, which is a question about the shelf rather than about what is on it - and
		// the only place it can be asked, the way a note's is. "No folder" is one of the answers rather
		// than a way of undoing the others: one in none is in a built-in folder, which is not nothing -
		// see FolderPlacement. A private shelf is filed like any other; its folder is outside the
		// sealed half.
		List<ScreenMenuGroup> groups = [new ScreenMenuGroup(null, entries)];
		if (_viewModel.CanEdit)
		{
			List<ScreenMenuEntry> folders =
			[
				new ScreenMenuEntry(
					_translations["No folder"],
					() => _viewModel.FileCommand.Execute(null),
					_viewModel.FolderId is null)
			];

			folders.AddRange(_viewModel.Folders.Select(folder => new ScreenMenuEntry(
				folder.Name,
				() => _viewModel.FileCommand.Execute(folder.LocalId),
				folder.LocalId == _viewModel.FolderId)));

			groups.Add(new ScreenMenuGroup(_translations["Folder"], folders));
		}

		Menu.ShowGroups(groups);
	}

	/// <summary>
	/// Typed so the item template's bindings back up to the page can be compiled - see the comment in the
	/// XAML about why they go through the page rather than naming the view model directly.
	/// </summary>
	public InventoryDetailViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		// Subscribed before the load, so the row a shelf was opened for is brought into view as soon as
		// there is one - a mark below the fold is a mark nobody sees.
		_viewModel.PropertyChanged += OnViewModelChanged;
		_viewModel.LoadCommand.Execute(null);
	}

	private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
	{
		if (args.PropertyName != nameof(InventoryDetailViewModel.PointedAtRow)
			|| _viewModel.PointedAtRow is not { } row)
		{
			return;
		}

		// Not animated: this is where the screen opens rather than somewhere it travels to, and a shelf
		// that scrolls itself under a thumb already on it is a shelf that fights back.
		Shelf.ScrollTo(row, position: ScrollToPosition.Center, animate: false);
	}

	/// <summary>Lets go of the edit lock as the screen leaves - see EditLock.</summary>
	protected override async void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.PropertyChanged -= OnViewModelChanged;
		await _viewModel.CloseAsync();
	}

	/// <summary>
	/// What a row's "⋯" opens. On the page rather than the view model because an action sheet is a
	/// page's own presentation - the same reason ConversationPage keeps its message menu here.
	/// </summary>
	public ICommand ShowItemMenuCommand { get; }

	private async Task ShowItemMenuAsync(InventoryItemRow? item)
	{
		if (item is null)
		{
			return;
		}

		var remove = _translations["Delete item"];
		var moveUp = _translations["Move up"];
		var moveDown = _translations["Move down"];
		var chosen = await DisplayActionSheetAsync(
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
