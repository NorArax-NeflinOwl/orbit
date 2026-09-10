using System.Collections.Specialized;
using System.Windows.Input;
using Orbit.Mobile.Localization;
using Microsoft.Maui.Layouts;
using Orbit.Maui.Controls;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Maui.Features.Calendar;

public partial class CalendarPage : ContentPage, ITitleMenu
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
		ShowTitleMenuCommand = new Command(ShowSortMenu);
		ShowCardMenuCommand = new Command<CalendarListEntry>(ShowCardMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		AddButton.Command = NewItemForm.Toggling(AddRow, AddField);
		_viewModel.DayBlocks.CollectionChanged += OnTheDayChanged;
		DayClock.SizeChanged += OnTheClockLaidOut;
	}

	/// <summary>
	/// The grid's day cells need a command that lives on the screen rather than on the cell, and a
	/// RelativeSource walks the visual tree - so it names the page and comes through here.
	/// </summary>
	public CalendarViewModel ViewModel => _viewModel;

	/// <summary>What order the list under the grid is read in - see CalendarListEntry.</summary>
	public ICommand ShowTitleMenuCommand { get; }

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
			placement: MenuPlacement.FromTheFoot);
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
	///
	/// Two groups, because it has always asked two different questions and used to run them together
	/// under one heading: what order to read the list in, and how much of it to read. The design draws
	/// a calendar menu of two groups as well, but names things this screen does not have - layers to
	/// switch on and off, and sharing a day - so it is the shape that is taken from it and not the
	/// entries, which would have to be invented.
	/// </summary>
	private void ShowSortMenu() => Menu.ShowGroups(
		[
			// The one in force is marked, because a menu of three with no answer among them leaves the
			// reader guessing what they are looking at.
			new ScreenMenuGroup(
				_translations["Sort"],
				[
					Order(_translations["By when"], CalendarListSortOrder.When),
					Order(_translations["By type"], CalendarListSortOrder.Type),
					Order(_translations["Alphabetical"], CalendarListSortOrder.Alphabetical)
				]),

			// What is over is left out of the list unless it is asked for, as in the browser - so the
			// same menu that says how to read it also says how much of it to read.
			new ScreenMenuGroup(
				_translations["Show"],
				[
					new(_translations["Everything, including what is over"],
						() =>
						{
							_viewModel.ShowsEverything = !_viewModel.ShowsEverything;
							ShowSortMenu();
						},
						_viewModel.ShowsEverything,
						staysOpen: true)
				])
		]);

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

	// The calendar used to shrink to one week as the list beneath it was scrolled past, and grow back
	// at the top. It does not any more: the week is one of the four views the reader asks for by name,
	// so a grid that changed size on its own was a second, silent answer to the same question - and one
	// nobody could ask for or refuse. See CalendarViewMode.

	/// <summary>
	/// Draws the chosen day: an hour rule behind, and every block where its placement says. Built here
	/// rather than bound, for the reason MapPage gives about its pins - where a block goes is a
	/// measurement, and keeping that out of the view model is what leaves the placement testable. What
	/// it draws is decided by <see cref="CalendarDayTimeline"/>, which is tested on its own.
	/// </summary>
	private void DrawTheDay()
	{
		HourLabels.Children.Clear();
		DayBlocks.Children.Clear();

		// The whole day, every time - see CalendarDayTimeline.FirstHour, which says why it is not
		// cropped to the hours something is in any more. What has no hour at all is a list under the
		// clock, drawn from the view model by the markup rather than here.
		DayClock.HeightRequest = (CalendarDayTimeline.LastHour - CalendarDayTimeline.FirstHour + 1) * HourHeight;
		DayClock.IsClippedToBounds = true;

		DrawTheHours();
		foreach (var block in _viewModel.DayBlocks)
		{
			DayBlocks.Children.Add(Block(block, CalendarDayTimeline.FirstHour));
		}

		OpenOnTheFirstThing();
	}

	/// <summary>
	/// How far down the clock this day should open, or null once it has been opened there. Twenty-four
	/// hours is a long column and most days start somewhere in the middle of it, so a day that opened
	/// at midnight would ask the reader to scroll past the small hours every time.
	/// </summary>
	private double? _openTheDayAt;

	/// <inheritdoc cref="_openTheDayAt"/>
	private void OpenOnTheFirstThing()
		=> _openTheDayAt = (_viewModel.FirstHourWorthLookingAt - CalendarDayTimeline.FirstHour) * HourHeight;

	/// <summary>
	/// Scrolls the day into place once the clock has actually been laid out at its new height. Asked
	/// for any earlier - in the same turn as the height was set, or on the next one - the scroll is
	/// clamped against the height the clock still had and lands at the top. The same trap
	/// TaskItemSummaryPage's map fell into, written up in android-ui-parity.md; here the height itself
	/// is the signal, so it is waited for rather than guessed at.
	/// </summary>
	private void OnTheClockLaidOut(object? sender, EventArgs eventArgs)
	{
		if (_openTheDayAt is not { } top || DayClock.Height <= 0)
		{
			return;
		}

		_openTheDayAt = null;
		Dispatcher.Dispatch(() => _ = DayScroll.ScrollToAsync(0, top, animated: false));
	}

	/// <summary>
	/// A label and a line on every hour, so a block has something to be read against. Drawn before the
	/// blocks, which is what puts it behind them.
	/// </summary>
	private void DrawTheHours()
	{
		const int firstHour = CalendarDayTimeline.FirstHour;

		for (var hour = firstHour; hour <= CalendarDayTimeline.LastHour; hour++)
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
