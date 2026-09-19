using System.ComponentModel;
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
	private readonly NavigationBarViewModel _bar;

	public Drawer()
	{
		InitializeComponent();
		_bar = IPlatformApplication.Current!.Services.GetRequiredService<NavigationBarViewModel>();
		BindingContext = _bar;

		// Watched only while this copy of the drawer is on screen. The view model outlives every page
		// that draws one - it is the single shared instance - so a subscription taken in the constructor
		// and never given back would keep all thirty pages' drawers alive behind it.
		Loaded += (_, _) => _bar.PropertyChanged += OnBarChanged;
		Unloaded += (_, _) => _bar.PropertyChanged -= OnBarChanged;
	}

	/// <summary>
	/// Opening the drawer puts the keyboard away - see <see cref="SoftKeyboard"/> for why that is the
	/// drawer's business. Only on the way open: closing it hands the screen back as it was.
	/// </summary>
	private void OnBarChanged(object? sender, PropertyChangedEventArgs args)
	{
		if (args.PropertyName is nameof(NavigationBarViewModel.IsDrawerOpen) && _bar.IsDrawerOpen)
		{
			SoftKeyboard.Dismiss(this);
		}
	}
}
