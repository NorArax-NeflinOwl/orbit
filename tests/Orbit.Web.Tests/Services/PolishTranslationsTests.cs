using System.Text.RegularExpressions;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// The Polish dictionary as a whole, rather than one string at a time. Two things about it can only be
/// found this way, and both take the app down rather than reading badly:
///
/// A duplicate key throws from the dictionary initializer, which runs at type-init - so the app dies the
/// moment somebody switches to Polish, on a page that has nothing to do with the duplicate. That has
/// nearly shipped more than once.
///
/// A value referring to a placeholder its English does not supply throws FormatException when that line
/// is written. Fewer placeholders than the English is allowed and deliberate: Polish plurals do not map
/// onto an English "list"/"lists", and folding the count into the sentence is better Polish than
/// pasting an English word into it.
/// </summary>
public sealed class PolishTranslationsTests
{
    [Fact]
    public void The_dictionary_can_be_built_at_all()
    {
        // Reading anything from it runs the initializer, which is where a duplicate key would throw.
        Assert.NotEmpty(PolishTranslations.ByEnglish);
    }

    [Fact]
    public void Nothing_is_translated_to_nothing()
    {
        var blanks = PolishTranslations.ByEnglish
            .Where(pair => string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => pair.Key)
            .ToList();

        Assert.Empty(blanks);
    }

    [Fact]
    public void No_translation_reaches_for_a_value_its_English_does_not_supply()
    {
        var wrong = PolishTranslations.ByEnglish
            .Where(pair => HighestPlaceholder(pair.Value) > HighestPlaceholder(pair.Key))
            .Select(pair => $"'{pair.Key}' -> '{pair.Value}'")
            .ToList();

        Assert.Empty(wrong);
    }

    [Fact]
    public void Every_line_written_in_Polish_can_actually_be_written()
    {
        // The same check from the other side: formatting each entry with as many arguments as its
        // English asks for must not throw, whatever the translation does with them.
        var translations = InPolish();
        var arguments = new object[] { "one", "two", "three", "four" };

        foreach (var english in PolishTranslations.ByEnglish.Keys)
        {
            var needed = HighestPlaceholder(english) + 1;
            var written = translations.Format(english, arguments.Take(needed).ToArray());

            Assert.False(string.IsNullOrWhiteSpace(written));
        }
    }

    [Fact]
    public void A_key_with_no_translation_still_reads_as_the_English_it_already_is()
    {
        // The fallback the whole scheme rests on - it is what makes translating a page at a time safe.
        Assert.Equal("Not translated yet", InPolish()["Not translated yet"]);
    }

    private static Translations InPolish()
    {
        var translations = new Translations(new StubJSRuntime());
        translations.SetLanguageAsync(AppLanguage.Polish).GetAwaiter().GetResult();
        return translations;
    }

    /// <summary>The largest {n} in the text, or -1 when there is none.</summary>
    private static int HighestPlaceholder(string text)
        => Regex.Matches(text, @"\{(\d+)\}")
            .Select(match => int.Parse(match.Groups[1].Value))
            .DefaultIfEmpty(-1)
            .Max();
}
