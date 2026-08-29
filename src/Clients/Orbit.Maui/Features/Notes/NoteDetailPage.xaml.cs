using System.Windows.Input;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Notes;

namespace Orbit.Maui.Features.Notes;

public partial class NoteDetailPage : ContentPage
{
	private readonly NoteDetailViewModel _viewModel;
	private readonly Translations _translations;

	public NoteDetailPage(NoteDetailViewModel viewModel, Translations translations)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_translations = translations;
		ShowLineMenuCommand = new Command<NoteLineRow>(line => _ = ShowLineMenuAsync(line));
	}

	/// <summary>Typed so the navigator can hand the page its note without casting the binding context.</summary>
	public NoteDetailViewModel ViewModel => _viewModel;

	/// <summary>
	/// What a line's "⋯" opens. On the page rather than the view model because an action sheet is a
	/// page's own presentation - the same reason ConversationPage keeps its message menu here.
	/// </summary>
	public ICommand ShowLineMenuCommand { get; }

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	/// <summary>Lets go of the note's edit lock as the screen leaves - see EditLock.</summary>
	protected override async void OnDisappearing()
	{
		base.OnDisappearing();
		await _viewModel.CloseAsync();
	}

	private async Task ShowLineMenuAsync(NoteLineRow? line)
	{
		if (line is null)
		{
			return;
		}

		var makeOrUnmake = line.IsChecklistItem
			? _translations["Make it an ordinary line"]
			: _translations["Make it a checklist item"];
		var remove = _translations["Delete line"];

		var chosen = await DisplayActionSheet(
			_translations["Line options"], _translations["Cancel"], remove, makeOrUnmake);

		if (chosen == makeOrUnmake)
		{
			_viewModel.ToggleChecklistCommand.Execute(line);
		}
		else if (chosen == remove)
		{
			_viewModel.RemoveLineCommand.Execute(line);
		}
	}
}
