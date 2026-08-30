using System.Globalization;
using Orbit.Localization;

namespace Orbit.Mobile.Localization;

/// <summary>Remembers which language the reader picked, across launches.</summary>
public interface ILanguageStore
{
    AppLanguage Read();

    void Write(AppLanguage language);
}

/// <summary>
/// The app's user-facing text in the reader's chosen language.
///
/// Reads the same dictionary Orbit.Web reads, keyed by the English text itself: <c>T["Add note"]</c>
/// says what it puts on screen, and a string with no translation falls back to the English it already
/// is rather than to a blank. That is what makes it safe to translate a screen at a time - and what
/// lets both clients share one dictionary despite showing different screens.
///
/// Deliberately does not set the thread's culture. Coordinates in a shared position, the ids in an API
/// path and the timestamps a log is parsed from are all formatted invariantly on purpose - a Polish
/// decimal comma in a coordinate is a different place. Only text a person reads follows the language,
/// through <see cref="DisplayCulture"/>.
/// </summary>
public sealed class Translations
{
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo PolishCulture = CultureInfo.GetCultureInfo("pl-PL");

    private readonly ILanguageStore _store;

    public Translations(ILanguageStore store)
    {
        _store = store;
        Language = store.Read();
    }

    /// <summary>Raised after the language changes, so whatever is on screen can be shown again in it.</summary>
    public event EventHandler? Changed;

    public AppLanguage Language { get; private set; }

    /// <summary>
    /// The culture dates and day names are written in - never used for anything a machine parses back.
    /// Reading an interface in Polish and being told "Monday, March 3" is only half a translation.
    /// </summary>
    public CultureInfo DisplayCulture => Language == AppLanguage.Polish ? PolishCulture : EnglishCulture;

    /// <summary>
    /// How a date is written here - "d.MM.yyyy" for a Polish reader, "M/d/yyyy" for an English one.
    ///
    /// Handed to controls that format a value themselves instead of being given a string. MAUI's
    /// DatePicker and TimePicker render against the phone's own culture rather than this one, so a
    /// Polish calendar showed "sierpień 2026" above a field reading "8/30/2026".
    /// </summary>
    public string DatePattern => DisplayCulture.DateTimeFormat.ShortDatePattern;

    /// <inheritdoc cref="DatePattern"/>
    public string TimePattern => DisplayCulture.DateTimeFormat.ShortTimePattern;

    public string this[string english]
        => Language == AppLanguage.Polish && PolishTranslations.ByEnglish.TryGetValue(english, out var polish)
            ? polish
            : english;

    /// <summary>
    /// A translated sentence with values dropped into it. The key keeps the whole sentence together
    /// with {0}-style placeholders rather than being glued from fragments, because word order differs
    /// between languages and a sentence assembled from pieces can only come out in English's.
    /// </summary>
    public string Format(string english, params object?[] arguments)
        => string.Format(DisplayCulture, this[english], arguments);

    public void SetLanguage(AppLanguage language)
    {
        if (Language == language)
        {
            return;
        }

        Language = language;
        _store.Write(language);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
