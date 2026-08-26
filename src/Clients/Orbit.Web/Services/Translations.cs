using System.Globalization;
using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// The app's user-facing text in the reader's chosen language.
///
/// Keyed by the English text itself rather than by invented identifiers: <c>T["Add note"]</c> reads as
/// what it puts on screen, a key that has no translation yet falls back to the English it already is
/// (rather than to a blank or a shouty MISSING_KEY), and adding a language is adding one dictionary
/// rather than editing every call site. The cost is that changing the English means updating the
/// dictionaries - which is the same work as changing a key, said once instead of twice.
///
/// Also decides how a date is written, through <see cref="DisplayCulture"/>. Reading an interface in
/// Polish and being told "Monday, March 3" is only half a translation.
///
/// It deliberately does not set the thread's culture to do that. Coordinates in a Google Maps URL, the
/// date stamps in a Google Calendar link, aria attributes and CSS values are all formatted against
/// InvariantCulture on purpose - a Polish decimal comma in a URL sends the reader somewhere else
/// entirely. Those stay invariant; only text a person reads follows the language.
/// </summary>
public sealed class Translations
{
    private const string StorageKey = "orbit-language";

    private readonly IJSRuntime _jsRuntime;

    public Translations(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>Raised after the language changes, so the layout can re-render everything below it.</summary>
    public event Action? Changed;

    public AppLanguage Language { get; private set; } = AppLanguage.English;

    /// <summary>
    /// The culture dates and day names are written in - never used for anything a machine parses back.
    /// A page formatting a coordinate or a URL keeps InvariantCulture, which is what stops a decimal
    /// comma from turning a link into a different place.
    /// </summary>
    public CultureInfo DisplayCulture
        => Language == AppLanguage.Polish ? PolishCulture : EnglishCulture;

    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo PolishCulture = CultureInfo.GetCultureInfo("pl-PL");

    /// <summary>
    /// The text for this language, or the English key itself when there is no translation for it. A
    /// missing entry therefore shows correct English rather than a hole, which is what makes it safe to
    /// translate the app a page at a time.
    /// </summary>
    public string this[string english]
        => Language == AppLanguage.Polish && PolishTranslations.ByEnglish.TryGetValue(english, out var polish)
            ? polish
            : english;

    public async Task InitializeAsync()
    {
        try
        {
            var stored = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            Language = stored == nameof(AppLanguage.Polish) ? AppLanguage.Polish : AppLanguage.English;
        }
        catch (JSException)
        {
            // A browser with storage blocked (private windows, embedded webviews) gets English, which is
            // what an unanswered question should give.
            Language = AppLanguage.English;
        }
    }

    public async Task SetLanguageAsync(AppLanguage language)
    {
        Language = language;
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, language.ToString());
        }
        catch (JSException)
        {
            // Applies for this session even if it can't be remembered for the next one.
        }
        finally
        {
            Changed?.Invoke();
        }
    }
}

/// <summary>The languages Orbit's own interface is written in.</summary>
public enum AppLanguage
{
    English,
    Polish
}
