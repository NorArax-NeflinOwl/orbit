using Orbit.Mobile.Localization;

namespace Orbit.Maui.Controls;

/// <summary>
/// Puts a translated string into XAML: <c>Text="{controls:Translate 'New note'}"</c>.
///
/// The argument is the English text rather than an invented key, so the markup still says what appears
/// on screen and an untranslated string shows correct English - see <see cref="Translations"/>.
///
/// Resolved once, when the page is built. That is enough because AppNavigator replaces the whole page
/// on every navigation, so changing the language and going anywhere shows it - and the language screen
/// re-shows itself for the case where the reader stays put.
/// </summary>
[ContentProperty(nameof(Text))]
public sealed class TranslateExtension : IMarkupExtension<string>
{
	public string Text { get; set; } = string.Empty;

	public string ProvideValue(IServiceProvider serviceProvider)
		=> IPlatformApplication.Current!.Services.GetRequiredService<Translations>()[Text];

	object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
