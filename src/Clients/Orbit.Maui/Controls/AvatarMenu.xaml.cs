using System.ComponentModel;
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
	private readonly NavigationBarViewModel _bar;

	public AvatarMenu()
	{
		InitializeComponent();
		_bar = IPlatformApplication.Current!.Services.GetRequiredService<NavigationBarViewModel>();
		BindingContext = _bar;

		// Only while on screen, for the reason <see cref="Drawer"/> spells out: the view model is shared
		// and outlives every page that draws one of these.
		Loaded += (_, _) => _bar.PropertyChanged += OnBarChanged;
		Unloaded += (_, _) => _bar.PropertyChanged -= OnBarChanged;
	}

	/// <summary>
	/// Opening the menu puts the keyboard away, the same as the drawer does and for the same reason -
	/// see <see cref="SoftKeyboard"/>.
	/// </summary>
	private void OnBarChanged(object? sender, PropertyChangedEventArgs args)
	{
		if (args.PropertyName is nameof(NavigationBarViewModel.IsMenuOpen) && _bar.IsMenuOpen)
		{
			SoftKeyboard.Dismiss(this);
		}
	}
}
