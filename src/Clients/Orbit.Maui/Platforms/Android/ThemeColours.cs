namespace Orbit.Maui.Platform;

/// <summary>
/// One of Orbit's own colours, read off the application's resources by the name Colors.xaml gives it.
///
/// The three handler mappers that dress Android's own controls - a field's box, a switch's track, a
/// stepper's buttons - all need this and none of them can use an AppThemeBinding: they are painting
/// platform drawables rather than setting MAUI properties. Shared so that "which grey is the hairline"
/// is answered in one place.
/// </summary>
internal static class ThemeColours
{
	public static bool IsDark => Application.Current?.RequestedTheme == AppTheme.Dark;

	/// <summary>The hairline every box, track and edge in Orbit is drawn with.</summary>
	public static Color Hairline => Look(IsDark ? "CardStrokeDark" : "CardStrokeLight");

	/// <summary>The lifted surface a card, a field and a menu panel all sit on.</summary>
	public static Color Surface => Look(IsDark ? "SurfaceDark" : "SurfaceLight");

	/// <summary>
	/// What the whole page sits on, which is also what the status bar takes: Orbit's own bar sits
	/// directly beneath it on the same ground, and a band of a different colour above it reads as a
	/// second bar. Asked for here rather than written into MainActivity, which is how three copies of
	/// this colour came to disagree with Colors.xaml in the first place.
	/// </summary>
	public static Color PageBackground => Look(IsDark ? "PageBackgroundDark" : "PageBackgroundLight");

	/// <summary>The line under a title - what a control's own words are written in.</summary>
	public static Color SubtleText => Look(IsDark ? "SubtleTextDark" : "SubtleTextLight");

	public static Color Look(string key)
		=> Application.Current?.Resources.TryGetValue(key, out var value) is true && value is Color colour
			? colour
			: Colors.Transparent;
}
