using Orbit.Mobile.Screens.Navigation;

namespace Orbit.Maui.Controls;

/// <summary>
/// The panel the top bar's three lines open: every section, with its name.
///
/// A separate control from <see cref="NavigationBar"/> rather than part of it, and drawn by the page
/// rather than by the bar - exactly as <see cref="AvatarMenu"/> is, and for the same reason: a panel
/// declared inside the bar is clipped to the bar's own height, and a drawer eight rows tall inside a
/// 56pt bar is not a drawer.
///
/// Resolves the one shared <see cref="NavigationBarViewModel"/> for itself, so the bar and the drawer
/// cannot disagree about whether the drawer is open.
/// </summary>
public partial class Drawer : ContentView
{
	public Drawer()
	{
		InitializeComponent();
		BindingContext = IPlatformApplication.Current!.Services.GetRequiredService<NavigationBarViewModel>();
	}

	/// <summary>
	/// The licence, opened in whatever the phone uses to read a page. A handler rather than a command,
	/// because opening a URL is the platform's job rather than the view model's.
	/// </summary>
	private async void OnLicenseTapped(object? sender, TappedEventArgs eventArgs)
	{
		if (BindingContext is NavigationBarViewModel viewModel)
		{
			await Launcher.Default.OpenAsync(viewModel.LicenseUrl);
		}
	}
}
