using Android.Graphics.Drawables;
using Android.Widget;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Orbit.Maui.Platform;

/// <summary>
/// Draws Orbit's text box on Android's own fields.
///
/// Every input in the design is a box: nothing but a hairline around it, a 4px radius and 9x12 of room
/// inside. Android draws a line under the text instead, in the Material accent, and MAUI has no border
/// on `Entry` to say otherwise - so a form that reads as a form in the design read as a list of
/// underlined words here.
///
/// Applied through the handler mappers rather than by wrapping every field in a Border, because there
/// are well over a hundred of them and one that was missed would be the odd one out.
/// </summary>
internal static class FieldBox
{
	/// <summary>1px border, 4px radius - the design's own radius - and 9px 12px of padding.</summary>
	private const float Radius = 4;
	private const float BorderWidth = 1;
	private const float PaddingAcross = 12;
	private const float PaddingDown = 9;

	public static void DrawOnEveryField()
	{
		// Appended under properties of the field's own rather than a name of ours: MAUI runs every key
		// once when a field is created, and again whenever that property changes. Keyed to our own name
		// instead, MAUI's own mapper would paint over the box the moment anything else redrew.
		//
		// Two keys rather than one, and TextColor is the load-bearing half. A theme switch has to redraw
		// the box, because the hairline is a different colour in each theme - and it used to be the
		// background that noticed, since the implicit styles set that through an AppThemeBinding. They
		// set it to a flat Transparent now, which never changes and so never tells anyone anything.
		// TextColor is still a pair, so it is what carries the news.
		foreach (var key in new[] { nameof(IView.Background), nameof(ITextStyle.TextColor) })
		{
			EntryHandler.Mapper.AppendToMapping(key, (handler, view) => Box(handler.PlatformView, view));
			EditorHandler.Mapper.AppendToMapping(key, (handler, view) => Box(handler.PlatformView, view));
			SearchBarHandler.Mapper.AppendToMapping(key, (handler, view) => Box(handler.PlatformView, view));
			PickerHandler.Mapper.AppendToMapping(key, (handler, view) => Box(handler.PlatformView, view));
			DatePickerHandler.Mapper.AppendToMapping(key, (handler, view) => Box(handler.PlatformView, view));
			TimePickerHandler.Mapper.AppendToMapping(key, (handler, view) => Box(handler.PlatformView, view));
		}
	}

	private static void Box(Android.Views.View? field, IView asked)
	{
		if (field is null || asked is not VisualElement element)
		{
			return;
		}

		// A field that has said it is not a box - see Controls/BareField.cs.
		if (Orbit.Maui.Controls.BareField.GetIsBare(element))
		{
			field.Background = null;
			return;
		}

		var density = field.Context?.Resources?.DisplayMetrics?.Density ?? 1;
		var box = new GradientDrawable();
		box.SetShape(ShapeType.Rectangle);
		box.SetCornerRadius(Radius * density);
		box.SetStroke((int)Math.Round(BorderWidth * density), ThemeColours.Hairline.ToPlatform());
		// The page shows through, which is the point: a field is an outline drawn on the ground rather
		// than a panel resting on it. A screen that wants a fill still gets one by asking for it.
		box.SetColor((element.BackgroundColor ?? Colors.Transparent).ToPlatform());

		field.Background = box;

		var across = (int)Math.Round(PaddingAcross * density);
		var down = (int)Math.Round(PaddingDown * density);
		field.SetPadding(across, down, across, down);

		// A search field on Android puts its own icon and clear button inside the box; left as they are,
		// they sit on top of the padding above. Nothing else needs undoing.
		if (field is SearchView search)
		{
			search.SetPadding(0, 0, 0, 0);
		}
	}

}
