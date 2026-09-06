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
		if (args.PropertyName is nameof(ScreenMenu.OpensUpwards) or nameof(ScreenMenu.IsOpen))
		{
			Place();
		}
	}

	private void Place()
	{
		var upwards = _menu?.OpensUpwards is true;
		Panel.VerticalOptions = upwards ? LayoutOptions.End : LayoutOptions.Start;

		// Clear of the bar along the top when it hangs from there, and clear of the rail when it opens
		// upwards out of one - the same 4px gap the web leaves between a trigger and its panel, plus
		// the height of the furniture it has to clear.
		Panel.Margin = upwards ? new Thickness(12, 12, 12, 68) : new Thickness(12, 58, 12, 12);
	}
}
