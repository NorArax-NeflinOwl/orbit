using Android.Graphics.Drawables;
using Android.Widget;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Orbit.Maui.Platform;

/// <summary>
/// Draws Orbit.Web's text box on Android's own fields.
///
/// Every input in the browser is a box: the lifted surface, a hairline around it, an 8px radius and
/// 9x12 of room inside (see app.css's `input[type=...], textarea, select`). Android draws a line under
/// the text instead, in the Material accent, and MAUI has no border on `Entry` to say otherwise - so a
/// form that reads as a form in the browser read as a list of underlined words here.
///
/// Applied through the handler mappers rather than by wrapping every field in a Border, because there
/// are well over a hundred of them and one that was missed would be the odd one out. Resolved against
/// the theme in force when the field is created: MAUI does not re-run a mapper when the theme changes,
/// and it does not have to - AppNavigator replaces the page on every navigation, and the screen that
/// changes the theme re-shows itself, which is the same argument TranslateExtension already makes for
/// the language.
/// </summary>
internal static class FieldBox
{
	/// <summary>app.css: 1px border, 8px radius, 9px 12px of padding.</summary>
	private const float Radius = 8;
	private const float BorderWidth = 1;
	private const float PaddingAcross = 12;
	private const float PaddingDown = 9;

	public static void DrawOnEveryField()
	{
		EntryHandler.Mapper.AppendToMapping(nameof(FieldBox), (handler, view) => Box(handler.PlatformView, view));
		EditorHandler.Mapper.AppendToMapping(nameof(FieldBox), (handler, view) => Box(handler.PlatformView, view));
		SearchBarHandler.Mapper.AppendToMapping(nameof(FieldBox), (handler, view) => Box(handler.PlatformView, view));
		PickerHandler.Mapper.AppendToMapping(nameof(FieldBox), (handler, view) => Box(handler.PlatformView, view));
		DatePickerHandler.Mapper.AppendToMapping(nameof(FieldBox), (handler, view) => Box(handler.PlatformView, view));
		TimePickerHandler.Mapper.AppendToMapping(nameof(FieldBox), (handler, view) => Box(handler.PlatformView, view));
	}

	private static void Box(Android.Views.View? field, IView asked)
	{
		// A field asked to be transparent has already said it is not a box: a note's lines are written
		// in Entries so they can be corrected where they are read, and a note drawn as a stack of boxes
		// is a form, not a note. Transparency rather than "has a background of its own" - the implicit
		// Entry style gives every field the lifted surface, so having one says nothing, and IsSet does
		// not tell a style's value from a local one.
		if (field is null || asked is not VisualElement element)
		{
			return;
		}

		if (element.BackgroundColor is { Alpha: 0 })
		{
			// Nothing at all, not even Android's line: a note's line is text somebody can correct, and
			// the browser draws no box and no rule under it either.
			field.Background = null;
			return;
		}

		var density = field.Context?.Resources?.DisplayMetrics?.Density ?? 1;
		var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

		var box = new GradientDrawable();
		box.SetShape(ShapeType.Rectangle);
		box.SetCornerRadius(Radius * density);
		box.SetStroke((int)Math.Round(BorderWidth * density), Look(isDark ? "CardStrokeDark" : "CardStrokeLight").ToPlatform());
		box.SetColor(Look(isDark ? "SurfaceDark" : "SurfaceLight").ToPlatform());

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

	private static Color Look(string key)
		=> Application.Current?.Resources.TryGetValue(key, out var value) is true && value is Color colour
			? colour
			: Colors.Transparent;
}
