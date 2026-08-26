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
/// Deliberately does not touch <see cref="System.Globalization.CultureInfo"/>. Several pages format
/// dates against a fixed culture on purpose (see Dashboard's DisplayCulture and its siblings), and
/// switching the thread's culture would silently change all of those too. Translating the words is this
/// class's job; how a date is written is a separate decision.
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
