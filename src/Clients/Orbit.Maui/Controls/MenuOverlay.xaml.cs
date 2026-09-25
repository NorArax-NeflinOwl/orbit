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
		SizeChanged += (_, _) => CapTheHeight();
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

		// A row's menu is most often opened from a screen that was being typed on - see
		// <see cref="SoftKeyboard"/> for what the keyboard does to a panel that has to fit above it.
		if (args.PropertyName is nameof(ScreenMenu.IsOpen) && _menu?.IsOpen is true)
		{
			SoftKeyboard.Dismiss(this);
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
		CapTheHeight();
	}

	/// <summary>
	/// How tall the entries may be before they scroll instead of growing: what the overlay has, less
	/// the margins the panel is placed with and the panel's own padding. Without a cap the panel sizes
	/// itself to its contents - it is aligned to one edge rather than filling - so a menu longer than
	/// the screen ran off the bottom of it with nothing saying so, which is how the notes' own menu was
	/// found cut off on 2026-09-24.
	///
	/// Set from the height the overlay actually has rather than from the display's, because the two are
	/// not the same: the bar, the ad strip at the foot and the system's own insets are all outside it.
	/// A floor of 160 so that a panel measured before the first layout is still worth opening.
	/// </summary>
	private void CapTheHeight()
	{
		if (Height <= 0)
		{
			return;
		}

		const double panelPadding = 24;
		Entries.MaximumHeightRequest = Math.Max(160, Height - Panel.Margin.Top - Panel.Margin.Bottom - panelPadding);
	}
}
