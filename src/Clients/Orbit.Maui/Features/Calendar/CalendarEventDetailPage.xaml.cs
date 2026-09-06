using System.Windows.Input;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Maui.Features.Calendar;

public partial class CalendarEventDetailPage : ContentPage
{
	private readonly CalendarEventDetailViewModel _viewModel;
	private readonly Translations _translations;

	public CalendarEventDetailPage(CalendarEventDetailViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: these two are bound from the static part of the tree,
		// which is built there and reads each property once. A command assigned afterwards is read as
		// null and never looked at again - the page's own buttons then do nothing at all. (The chat's
		// message menu gets away with the other order only because its binding is inside a DataTemplate,
		// which is not built until a row exists.)
		_viewModel = viewModel;
		_translations = translations;
		ShowEventMenuCommand = new Command(ShowEventMenu);
		ChooseReminderCommand = new Command(() => _ = ChooseReminderAsync());
		ChooseReminderChannelCommand = new Command(() => _ = ChooseChannelAsync(
			_translations["Notification as the event approaches"],
			channel => _viewModel.ReminderChannel = channel));

		InitializeComponent();
		BindingContext = viewModel;
	}

	/// <summary>Typed so the navigator can hand the page its event without casting the binding context.</summary>
	public CalendarEventDetailViewModel ViewModel => _viewModel;

	/// <summary>What the rail's three dots open.</summary>
	public ICommand ShowEventMenuCommand { get; }

	/// <summary>The panel they draw - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	/// <summary>
	/// What else can be done to the event, which used to be a row of words at the end of a long form -
	/// out of reach wherever the reading happened to stop, which is the whole reason the rail exists.
	/// </summary>
	private void ShowEventMenu()
	{
		List<ScreenMenuEntry> entries =
		[
			new(_translations["Back to calendar"], () => _viewModel.GoBackCommand.Execute(null))
		];

		if (_viewModel.CanEdit)
		{
			entries.Add(new ScreenMenuEntry(_translations["Delete event"], () => _viewModel.DeleteCommand.Execute(null)));
		}

		// Only once there is one, and here rather than in the account's menu: a history belongs to the
		// thing it is the history of.
		if (_viewModel.HasHistory)
		{
			entries.Add(new ScreenMenuEntry(_translations["History"], () => _viewModel.GoToHistoryCommand.Execute(null)));
		}

		Menu.Show(entries, opensUpwards: true);
	}

	/// <summary>
	/// The three choices this form offers, each as a sheet. Not pickers: iOS docks a picker's wheel at
	/// the bottom of the screen, which on a form this long covers Save - and the wheel's own "Done"
	/// lands on top of that button, so the two cannot both be pressed.
	/// </summary>
	public ICommand ChooseReminderCommand { get; }


	public ICommand ChooseReminderChannelCommand { get; }

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

	private async Task ChooseReminderAsync()
	{
		var names = _viewModel.ReminderChoices.Select(choice => choice.Name).ToArray();
		var chosen = await DisplayActionSheet(
			_translations["Add reminder"], _translations["Cancel"], destruction: null, names);

		if (_viewModel.ReminderChoices.FirstOrDefault(choice => choice.Name == chosen) is { } reminder)
		{
			_viewModel.ReminderToAdd = reminder;
		}
	}

	private async Task ChooseChannelAsync(string title, Action<NotificationChannelChoice> chose)
	{
		var names = _viewModel.Channels.Select(channel => channel.Name).ToArray();
		var chosen = await DisplayActionSheet(title, _translations["Cancel"], destruction: null, names);

		if (_viewModel.Channels.FirstOrDefault(channel => channel.Name == chosen) is { } channel)
		{
			chose(channel);
			_viewModel.SaveCommand.Execute(null);
		}
	}

	/// <summary>
	/// Leaving the app is a platform call, so the page makes it. The view model built the URL, which is
	/// the half worth testing - the same split as the action sheets above.
	/// </summary>
	private async void OnAddToGoogleCalendarClicked(object? sender, EventArgs e)
		=> await Launcher.Default.OpenAsync(_viewModel.AddToGoogleCalendarUrl);

	private async void OnOpenLocationInGoogleMapsClicked(object? sender, EventArgs e)
	{
		if (_viewModel.LocationInGoogleMapsUrl is { } url)
		{
			await Launcher.Default.OpenAsync(url);
		}
	}

	private async void OnOpenDirectionsClicked(object? sender, EventArgs e)
	{
		if (_viewModel.LocationDirectionsUrl is { } url)
		{
			await Launcher.Default.OpenAsync(url);
		}
	}
}
