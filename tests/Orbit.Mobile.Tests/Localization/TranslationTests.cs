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
