using System.Collections.Specialized;
using Microsoft.Maui.Layouts;
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

	public CalendarPage(CalendarViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_viewModel.DayBlocks.CollectionChanged += OnTheDayChanged;
		_viewModel.AllDayBlocks.CollectionChanged += OnTheDayChanged;
	}

	/// <summary>
	/// The grid's day cells need a command that lives on the screen rather than on the cell, and a
	/// RelativeSource walks the visual tree - so it names the page and comes through here.
	/// </summary>
	public CalendarViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	private void OnTheDayChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs) => DrawTheDay();

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

		var (firstHour, lastHour) = CalendarDayTimeline.HoursWorthDrawing(_viewModel.DayBlocks);
		DayClock.HeightRequest = (lastHour - firstHour + 1) * HourHeight;

		DrawTheHours(firstHour, lastHour);
		foreach (var block in _viewModel.DayBlocks)
		{
			DayBlocks.Children.Add(Block(block, firstHour));
		}
	}

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
