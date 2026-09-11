using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Features.Chat;

public partial class GroupDetailPage : ContentPage
{
	private readonly GroupDetailViewModel _viewModel;

	public GroupDetailPage(GroupDetailViewModel viewModel, Translations translations)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		// The same question the group list asks before anybody leaves - see GroupLeaveDialog.
		viewModel.AskBeforeLeaving = question => GroupLeaveDialog.AskAsync(this, question, translations);
	}

	/// <summary>Typed so the member rows' bindings back up to the page can be compiled.</summary>
	public GroupDetailViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}
