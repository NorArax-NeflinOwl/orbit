using System.Globalization;
using Orbit.Localization;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Localization;

/// <summary>
/// The app's text in the reader's language. Shares Orbit.Web's dictionary, keyed by the English text
/// itself - which is what makes an untranslated string show correct English rather than a hole.
/// </summary>
public sealed class TranslationTests
{
    [Fact]
    public void English_is_what_the_key_already_says()
    {
        var translations = Build(AppLanguage.English);

        Assert.Equal("Notes", translations["Notes"]);
    }

    [Fact]
    public void Polish_comes_from_the_dictionary_the_web_reads()
    {
        var translations = Build(AppLanguage.Polish);

        Assert.Equal("Notatki", translations["Notes"]);
    }

    [Fact]
    public void A_string_nobody_has_translated_stays_correct_English()
    {
        // The whole reason the key is the English text. A missing entry is a gap in the translation,
        // not a hole in the screen, which is what makes it safe to translate a screen at a time.
        var translations = Build(AppLanguage.Polish);

        Assert.Equal("Something nobody wrote down", translations["Something nobody wrote down"]);
    }

    [Fact]
    public void A_sentence_keeps_its_values_in_the_right_places()
    {
        var translations = Build(AppLanguage.English);

        Assert.Equal("2 of 5", translations.Format("{0} of {1}", 2, 5));
    }

    [Fact]
    public void Dates_follow_the_language_too()
    {
        // Reading an interface in Polish and being told "Monday, March 3" is only half a translation.
        Assert.Equal(CultureInfo.GetCultureInfo("pl-PL"), Build(AppLanguage.Polish).DisplayCulture);
        Assert.Equal(CultureInfo.GetCultureInfo("en-US"), Build(AppLanguage.English).DisplayCulture);
    }

    /// <summary>
    /// A control that formats its own value gets the pattern rather than a string - MAUI's DatePicker
    /// and TimePicker render against the phone's culture, not this one, so a Polish calendar reading
    /// "sierpień 2026" sat above a field reading "8/30/2026". Found on a device.
    /// </summary>
    [Fact]
    public void A_picker_is_told_how_the_reader_writes_a_date()
    {
        var polish = Build(AppLanguage.Polish);
        var english = Build(AppLanguage.English);

        Assert.Equal("d.MM.yyyy", polish.DatePattern);
        Assert.Equal("HH:mm", polish.TimePattern);
        Assert.NotEqual(polish.DatePattern, english.DatePattern);
    }

    /// <summary>
    /// The patterns are numeric and separator-only, which matters because the control renders them
    /// against whatever culture the phone is set to: a pattern naming a month or an AM/PM designator
    /// would come out in a third language on a phone set to a third language.
    /// </summary>
    [Fact]
    public void The_pattern_says_the_same_thing_whatever_the_phone_is_set_to()
    {
        var written = new DateTime(2026, 8, 30, 15, 0, 0);
        var polish = Build(AppLanguage.Polish);

        Assert.Equal(
            written.ToString(polish.DatePattern, CultureInfo.GetCultureInfo("de-DE")),
            written.ToString(polish.DatePattern, CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal(
            written.ToString(polish.TimePattern, CultureInfo.GetCultureInfo("de-DE")),
            written.ToString(polish.TimePattern, CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void The_choice_survives_a_restart()
    {
        var store = new InMemoryLanguageStore();
        new Translations(store).SetLanguage(AppLanguage.Polish);

        Assert.Equal(AppLanguage.Polish, new Translations(store).Language);
    }

    [Fact]
    public void Choosing_the_language_already_in_use_says_nothing()
    {
        // Otherwise every visit to the menu would rebuild the screen for no reason.
        var translations = Build(AppLanguage.English);
        var changes = 0;
        translations.Changed += (_, _) => changes++;

        translations.SetLanguage(AppLanguage.English);

        Assert.Equal(0, changes);
    }

    private static Translations Build(AppLanguage language)
    {
        var store = new InMemoryLanguageStore();
        store.Write(language);
        return new Translations(store);
    }
}
