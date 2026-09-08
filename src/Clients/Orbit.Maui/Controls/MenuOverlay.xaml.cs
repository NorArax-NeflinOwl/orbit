using Orbit.Mobile.Screens;

namespace Orbit.Maui.Controls;

/// <summary>
/// Draws whatever <see cref="ScreenMenu"/> a screen has open - see the markup for the shape, and
/// Orbit.Web's OverflowMenu for the same panel on the other client. One per page, bound to that page's
/// menu, and invisible until something fills it.
/// </summary>
public partial class MenuOverlay : ContentView
{
	public MenuOverlay()
	{
		InitializeComponent();
		BindingContextChanged += (_, _) => Follow(BindingContext as ScreenMenu);
	}

	private ScreenMenu? _menu;

	/// <summary>
	/// Which edge the panel takes. Watched rather than bound, because both of the layout options it
	/// sets are one property each on the panel and a converter apiece would say less than this does.
	/// </summary>
	private void Follow(ScreenMenu? menu)
	{
		if (_menu is not null)
		{
			_menu.PropertyChanged -= OnMenuChanged;
		}

		_menu = menu;

		if (_menu is not null)
		{
			_menu.PropertyChanged += OnMenuChanged;
			Place();
		}
	}

	private void OnMenuChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
	{
		if (args.PropertyName is nameof(ScreenMenu.Placement) or nameof(ScreenMenu.IsOpen))
		{
			Place();
		}
	}

	/// <summary>
	/// The two shapes a menu takes. A screen's own menu is centred under its name in the bar, at the
	/// width the design gives it; a row's opens upwards out of the foot of the screen, full width,
	/// because hanging it off a row low down the page would open it into the ground.
	///
	/// 56 clears the bar; 20 keeps the foot menu off the very bottom edge, where a thumb resting on the
	/// phone would be over the first entry.
	/// </summary>
	private void Place()
	{
		var fromTheFoot = _menu?.Placement is MenuPlacement.FromTheFoot;

		Panel.VerticalOptions = fromTheFoot ? LayoutOptions.End : LayoutOptions.Start;
		Panel.HorizontalOptions = fromTheFoot ? LayoutOptions.Fill : LayoutOptions.Center;
		Panel.WidthRequest = fromTheFoot ? -1 : 264;
		Panel.Margin = fromTheFoot ? new Thickness(12, 12, 12, 20) : new Thickness(12, 56, 12, 12);
	}
}
