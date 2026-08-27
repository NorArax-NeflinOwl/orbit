using Orbit.Mobile.Screens.Navigation;

namespace Orbit.Maui.Controls;

/// <summary>
/// The bar across the top of every signed-in page.
///
/// Resolves its own view model rather than taking one from the page it sits in. A page's view model has
/// nothing to do with the chrome around it, and threading a second one through every page's constructor
/// - and every page's test - to hold the same six navigation commands would be worse than this.
/// </summary>
public partial class NavigationBar : ContentView
{
	private readonly NavigationBarViewModel _viewModel;

	public NavigationBar()
	{
		InitializeComponent();
		_viewModel = IPlatformApplication.Current!.Services.GetRequiredService<NavigationBarViewModel>();
		BindingContext = _viewModel;
	}

	/// <summary>
	/// Loaded rather than the page's OnAppearing: a ContentView has no appearing of its own, and the
	/// unread badge is worth refreshing every time the bar comes back on screen.
	/// </summary>
	protected override void OnHandlerChanged()
	{
		base.OnHandlerChanged();
		if (Handler is not null)
		{
			_viewModel.LoadCommand.Execute(null);
		}
	}
}
