using System.Collections.Specialized;
using System.Windows.Input;
using Orbit.Mobile.Localization;
using Microsoft.Maui.Layouts;
using Orbit.Maui.Controls;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Maui.Features.Calendar;

public partial class CalendarPage : ContentPage
{
	/// <summary>
	/// How tall an hour is drawn. Twenty-four of these is the whole clock, which scrolls inside its own
	/// box: a day squeezed onto a phone screen leaves every block too thin to read or tap.
	/// </summary>
	private const double HourHeight = 46;

	private readonly CalendarViewModel _viewModel;
	private readonly Translations _translations;

	public CalendarPage(CalendarViewModel viewModel, Translations translations)
	{
		_translations = translations;
		// Assigned before InitializeComponent, which is where the binding to it is built - see
		// TaskListDetailPage for the same order and why it matters.
		ChooseSortOrderCommand = new Command(ShowSortMenu);
		ShowCardMenuCommand = new Command<CalendarListEntry>(ShowCardMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		AddButton.Command = NewItemForm.Toggling(AddRow, AddField);
		_viewModel.DayBlocks.CollectionChanged += OnTheDayChanged;
		_viewModel.AllDayBlocks.CollectionChanged += OnTheDayChanged;
	}

	/// <summary>
	/// The grid's day cells need a command that lives on the screen rather than on the cell, and a
	/// RelativeSource walks the visual tree - so it names the page and comes through here.
	/// </summary>
	public CalendarViewModel ViewModel => _viewModel;

	/// <summary>What order the list under the grid is read in - see CalendarListEntry.</summary>
	public ICommand ChooseSortOrderCommand { get; }

	/// <summary>What a card's three dots open. The same panel the header's do; only the entries differ.</summary>
	public ICommand ShowCardMenuCommand { get; }

	/// <summary>The panel both sets of dots draw - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	/// <summary>
	/// What a card offers besides opening it, which on this list is one thing: taking it off the
	/// calendar. No "Edit" entry, because on a phone pressing the card already opens what the browser's
	/// Edit opens - see info/android-ui-parity.md.
	/// </summary>
	private void ShowCardMenu(CalendarListEntry? entry)
	{
		if (entry is null)
		{
			return;
		}

		Menu.Show(
			[new ScreenMenuEntry(_translations["Delete"], () => _ = DeleteAsync(entry))],
			opensUpwards: true);
	}

	/// <summary>
	/// Asked first, as every delete in Orbit is. The two kinds are named differently because they are
	/// different things: an appointment is deleted, and a deadline is an entry coming off its list.
	/// </summary>
	private async Task DeleteAsync(CalendarListEntry entry)
	{
		var question = entry.IsEvent
			? _translations.Format("Delete event \"{0}\"?", entry.Name)
			: _translations.Format("Delete \"{0}\"?", entry.Name);

		if (await Confirmation.AskAsync(this, question, _translations["Delete"], _translations["Cancel"]))
		{
			await _viewModel.DeleteListedCommand.ExecuteAsync(entry);
		}
	}

	/// <summary>
	/// How to read the list under the grid, in Orbit's own panel rather than the platform's action
	/// sheet - the same menu Orbit.Web hangs off its Calendar header. It stays open while a reader
	/// tries one order and then another, and is asked again after each choice so the tick leaves
	/// whichever entry was carrying it.
	/// </summary>
	private void ShowSortMenu()
	{
		// The one in force is marked, because a menu of three with no answer among them leaves the
		// reader guessing what they are looking at.
		List<ScreenMenuEntry> entries =
		[
			Order(_translations["By when"], CalendarListSortOrder.When),
			Order(_translations["By type"], CalendarListSortOrder.Type),
			Order(_translations["Alphabetical"], CalendarListSortOrder.Alphabetical),

			// What is over is left out of the list unless it is asked for, as in the browser - so the
			// same menu that says how to read it also says how much of it to read.
			new(_translations["Everything, including what is over"],
				() =>
				{
					_viewModel.ShowsEverything = !_viewModel.ShowsEverything;
					ShowSortMenu();
				},
				_viewModel.ShowsEverything,
				staysOpen: true)
		];

		Menu.Show(entries, _translations["Sort"]);
	}

	private ScreenMenuEntry Order(string name, CalendarListSortOrder order) => new(
		name,
		() =>
		{
			_viewModel.SortOrder = order;
			ShowSortMenu();
		},
		_viewModel.SortOrder == order,
		staysOpen: true);

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	private void OnTheDayChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs) => DrawTheDay();

	/// <summary>
	/// The calendar gets out of the way as the list under it is read, and comes back when the reader
	/// returns to the top - see MinimisedCalendar for what is left of it. The offset rather than the
	/// first visible item: an item is a whole event row, and the grid should start standing aside as
	/// soon as the list moves at all.
	/// </summary>
	private void OnListScrolled(object? sender, ItemsViewScrolledEventArgs eventArgs)
	{
		var minimised = eventArgs.VerticalOffset > ScrollBeforeMinimising;
		if (minimised == _viewModel.IsMinimised)
		{
			return;
		}

		_viewModel.IsMinimised = minimised;
		DrawTheDay();
	}

	/// <summary>
	/// How far the list travels before the calendar stands aside. Enough that a thumb resting on the
	/// list does not flip it back and forth, and little enough that it is out of the way by the time
	/// anybody is reading.
	/// </summary>
	private const double ScrollBeforeMinimising = 24;

	/// <summary>
	/// Draws the chosen day: an hour rule behind, and every block where its placement says. Built here
	/// rather than bound, for the reason MapPage gives about its pins - where a block goes is a
	/// measurement, and keeping that out of the view model is what leaves the placement testable. What
	/// it draws is decided by <see cref="CalendarDayTimeline"/>, which is tested on its own.
	/// </summary>
	private void DrawTheDay()
	{
		AllDayRow.Children.Clear();
		HourLabels.Children.Clear();
		DayBlocks.Children.Clear();

		foreach (var block in _viewModel.AllDayBlocks)
		{
			AllDayRow.Children.Add(Chip(block));
		}

		var (firstHour, lastHour) = _viewModel.HoursOnShow;
		DayClock.HeightRequest = (lastHour - firstHour + 1) * HourHeight;
		// Minimised, the clock is one hour tall and a two-hour meeting would be drawn straight through
		// the list beneath it.
		DayClock.IsClippedToBounds = true;

		DrawTheHours(firstHour, lastHour);
		foreach (var block in _viewModel.DayBlocks.Where(block => IsWithin(block, firstHour, lastHour)))
		{
			DayBlocks.Children.Add(Block(block, firstHour));
		}
	}

	/// <summary>
	/// Whether any of this block falls in the hours being drawn. Everything, until the calendar is
	/// minimised to one hour - what does not reach that hour has nowhere to go.
	/// </summary>
	private static bool IsWithin(DayBlock block, int firstHour, int lastHour)
		=> block.StartMinute < (lastHour + 1) * 60 && block.StartMinute + block.Minutes > firstHour * 60;

	/// <summary>
	/// A label and a line on every hour, so a block has something to be read against. Drawn before the
	/// blocks, which is what puts it behind them.
	/// </summary>
	private void DrawTheHours(int firstHour, int lastHour)
	{
		for (var hour = firstHour; hour <= lastHour; hour++)
		{
			HourLabels.Children.Add(new Label
			{
				Text = $"{hour:00}:00",
				FontSize = 10,
				TextColor = Colour("SubtleTextLight", "SubtleTextDark"),
				VerticalOptions = LayoutOptions.Start,
				Margin = new Thickness(0, (hour - firstHour) * HourHeight - 6, 0, 0)
			});

			var line = new BoxView { Color = Colour("CardStrokeLight", "CardStrokeDark") };
			AbsoluteLayout.SetLayoutFlags(line, AbsoluteLayoutFlags.WidthProportional);
			AbsoluteLayout.SetLayoutBounds(line, new Rect(0, (hour - firstHour) * HourHeight, 1, 1));
			DayBlocks.Children.Add(line);
		}
	}

	/// <summary>
	/// One thing on the day, at the height it belongs and in the lane it was given. The lane is a
	/// fraction of the width rather than a number of pixels: how wide the day is drawn is not known
	/// here, and a proportional bound is how AbsoluteLayout is told to work it out.
	/// </summary>
	private View Block(DayBlock block, int firstHour)
	{
		var lanes = Math.Max(block.ColumnCount, 1);
		var drawn = new Border
		{
			BackgroundColor = Colour("SurfaceSubtleLight", "SurfaceSubtleDark"),
			Stroke = Colour("Primary", "PrimaryDark"),
			StrokeThickness = 1,
			Padding = new Thickness(6, 3),
			Margin = new Thickness(0, 0, 2, 2),
			Content = new VerticalStackLayout
			{
				Spacing = 0,
				Children =
				{
					new Label
					{
						Text = block.When,
						FontSize = 10,
						TextColor = Colour("SubtleTextLight", "SubtleTextDark")
					},
					new Label { Text = block.Title, FontSize = 13, LineBreakMode = LineBreakMode.TailTruncation }
				}
			},
			GestureRecognizers = { Opens(block) }
		};

		AbsoluteLayout.SetLayoutFlags(drawn, AbsoluteLayoutFlags.XProportional | AbsoluteLayoutFlags.WidthProportional);
		AbsoluteLayout.SetLayoutBounds(drawn, new Rect(
			// X runs 0 to 1 across the lanes, which is what a proportional X means: the first of two
			// lanes is 0, the second is 1 - not 0.5, because 1 is "as far right as it goes".
			lanes == 1 ? 0 : (double)block.Column / (lanes - 1),
			(block.StartMinute - firstHour * 60) * HourHeight / 60,
			1d / lanes,
			block.Minutes * HourHeight / 60));

		return drawn;
	}

	private View Chip(DayBlock block)
		=> new Border
		{
			BackgroundColor = Colour("SurfaceSubtleLight", "SurfaceSubtleDark"),
			StrokeThickness = 0,
			Padding = new Thickness(8, 4),
			Margin = new Thickness(0, 0, 6, 4),
			Content = new Label { Text = block.Title, FontSize = 12 },
			GestureRecognizers = { Opens(block) }
		};

	private TapGestureRecognizer Opens(DayBlock block)
		=> new() { Command = _viewModel.OpenBlockCommand, CommandParameter = block };

	/// <summary>
	/// The theme's own colour, read here because these views are built rather than declared and
	/// AppThemeBinding is a markup extension.
	/// </summary>
	private static Color Colour(string light, string dark)
	{
		var resources = Application.Current?.Resources;
		var key = Application.Current?.RequestedTheme == AppTheme.Dark ? dark : light;
		return resources is not null && resources.TryGetValue(key, out var colour) && colour is Color found
			? found
			: Colors.Transparent;
	}
}
