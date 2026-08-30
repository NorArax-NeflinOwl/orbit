using Orbit.Mobile.Localization;

namespace Orbit.Maui.Controls;

/// <summary>
/// The pattern a date is written in, for a picker that formats its own value:
/// <c>Format="{controls:DatePattern}"</c>.
///
/// MAUI's DatePicker renders against the phone's culture rather than the reader's chosen language, so
/// without this a Polish calendar reading "sierpień 2026" sat above a field reading "8/30/2026".
///
/// Resolved once, when the page is built - the same as <see cref="TranslateExtension"/>, and for the
/// same reason: changing the language rebuilds the page.
/// </summary>
public sealed class DatePatternExtension : IMarkupExtension<string>
{
	public string ProvideValue(IServiceProvider serviceProvider)
		=> IPlatformApplication.Current!.Services.GetRequiredService<Translations>().DatePattern;

	object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

/// <inheritdoc cref="DatePatternExtension"/>
public sealed class TimePatternExtension : IMarkupExtension<string>
{
	public string ProvideValue(IServiceProvider serviceProvider)
		=> IPlatformApplication.Current!.Services.GetRequiredService<Translations>().TimePattern;

	object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
