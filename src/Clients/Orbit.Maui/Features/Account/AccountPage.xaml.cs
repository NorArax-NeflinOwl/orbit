using Orbit.Mobile.Screens.Account;

namespace Orbit.Maui.Features.Account;

public partial class AccountPage : ContentPage
{
	private readonly AccountViewModel _viewModel;

	public AccountPage(AccountViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_viewModel.ThemeChanged += (_, theme) => Apply(theme);
		Apply(_viewModel.Theme);
	}

	/// <summary>The tab strip's buttons need the screen's command, and RelativeSource walks the visual tree.</summary>
	public AccountViewModel ViewModel => _viewModel;

	/// <summary>
	/// Turns the reader's choice into the app's theme. Unspecified means "follow the phone", which is
	/// what Orbit did before there was a choice at all.
	/// </summary>
	private static void Apply(ChosenTheme theme)
	{
		if (Application.Current is { } application)
		{
			application.UserAppTheme = theme switch
			{
				ChosenTheme.Light => AppTheme.Light,
				ChosenTheme.Dark => AppTheme.Dark,
				_ => AppTheme.Unspecified
			};
		}
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}
