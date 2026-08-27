using Orbit.Mobile.Screens.Navigation;

namespace Orbit.Maui.Controls;

/// <summary>
/// The sync line in the bottom-left corner. Resolves its own view model for the same reason
/// <see cref="NavigationBar"/> does: it is chrome, and a page's view model has nothing to do with it.
/// </summary>
public partial class StatusStrip : ContentView
{
	public StatusStrip()
	{
		InitializeComponent();
		BindingContext = IPlatformApplication.Current!.Services.GetRequiredService<StatusStripViewModel>();
	}
}
