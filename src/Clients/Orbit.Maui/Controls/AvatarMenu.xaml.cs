using Orbit.Mobile.Screens.Navigation;

namespace Orbit.Maui.Controls;

/// <summary>
/// The menu the avatar opens, as a page-covering overlay.
///
/// Separate from <see cref="NavigationBar"/> because of layout rather than taste: a bar that stretched
/// down the page to make room for its own menu demanded that height from the page's rows, and the
/// content beneath it was quietly clipped - found by scrolling to the end of the account screen and
/// discovering the last two buttons were not there.
///
/// Shares the bar's view model, which is a singleton for that reason: one is on screen at a time, and
/// the two halves have to agree about whether the menu is open.
/// </summary>
public partial class AvatarMenu : ContentView
{
	public AvatarMenu()
	{
		InitializeComponent();
		BindingContext = IPlatformApplication.Current!.Services.GetRequiredService<NavigationBarViewModel>();
	}

	/// <summary>
	/// Opens the licence outside the app. Handled here rather than as a command, because leaving the app
	/// is a platform call and this is the platform half - the same way the calendar screen opens a
	/// Google link.
	/// </summary>
	private async void OnLicenseTapped(object? sender, TappedEventArgs eventArgs)
	{
		if (BindingContext is NavigationBarViewModel viewModel)
		{
			await Launcher.Default.OpenAsync(viewModel.LicenseUrl);
		}
	}
}
