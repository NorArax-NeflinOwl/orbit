using System.Globalization;
using Orbit.Core.Users;
using Orbit.Mobile.Presence;

namespace Orbit.Maui.Controls;

/// <summary>
/// Turns presence into the colour of a dot. Lives here rather than on the view model because it is the
/// one part of presence that is purely how it looks - the view model decides the state, and this
/// decides the paint.
///
/// Takes either the phone's own <see cref="PresenceAppearance"/> or the server's PresenceStatus name
/// for somebody else, because the same dot has to mean the same thing in both places: green here,
/// yellow away, red not to be disturbed, grey not around.
///
/// The colours come from the app's palette rather than being invented at the call site, so the dark
/// theme's variants travel with them.
/// </summary>
public sealed class PresenceColorConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var key = value switch
		{
			PresenceAppearance.Active => "PresenceActive",
			PresenceAppearance.Idle => "PresenceIdle",
			PresenceAppearance.Unavailable => "PresenceUnavailable",
			nameof(PresenceStatus.Available) => "PresenceActive",
			nameof(PresenceStatus.Away) => "PresenceIdle",
			nameof(PresenceStatus.DoNotDisturb) => "PresenceUnavailable",
			_ => "PresenceOffline"
		};

		var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
		var resources = Application.Current?.Resources;
		return resources?[$"{key}{(isDark ? "Dark" : "Light")}"] ?? Colors.Grey;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException("A presence dot is never read back off the screen.");
}
