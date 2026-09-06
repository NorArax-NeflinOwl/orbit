using Android.Content.Res;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;

namespace Orbit.Maui.Platform;

/// <summary>
/// Gives a switch that is off a track somebody can see.
///
/// Orbit.Web draws one as a pill filled with `--border` and fills it with the accent when it is on
/// (see app.css's `.switch`). MAUI's Switch offers OnColor and ThumbColor and nothing for the track
/// while it is off, so Android paints that from the Material theme - which on the light theme is
/// near-white on a near-white card, so an off switch read as a grey dot floating on nothing.
///
/// Set as a two-state tint rather than a colour, because one drawable has to answer both states: the
/// accent while it is on, the hairline while it is not.
/// </summary>
internal static class SwitchTrack
{
	public static void DrawOnEverySwitch() => SwitchHandler.Mapper.AppendToMapping(
		nameof(ISwitch.TrackColor), (handler, _) => Tint(handler.PlatformView));

	private static void Tint(AndroidX.AppCompat.Widget.SwitchCompat? track)
	{
		if (track is null)
		{
			return;
		}

		var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

		track.TrackTintList = new ColorStateList(
			[[Android.Resource.Attribute.StateChecked], []],
			[
				Look("Accent").ToPlatform(),
				Look(isDark ? "CardStrokeDark" : "CardStrokeLight").ToPlatform()
			]);
	}

	private static Color Look(string key)
		=> Application.Current?.Resources.TryGetValue(key, out var value) is true && value is Color colour
			? colour
			: Colors.Transparent;
}
