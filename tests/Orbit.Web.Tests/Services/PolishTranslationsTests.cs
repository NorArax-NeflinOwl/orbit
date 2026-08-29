using System.Text.RegularExpressions;
using Orbit.Localization;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// The Polish dictionary as a whole, rather than one string at a time. Some of what can be wrong with it
/// is invisible from any single entry:
///
/// A key written twice is the quiet one. The dictionary is built from indexer initialisers, which
/// <i>overwrite</i> rather than throw, so the second wins and the first leaves no trace anywhere in the
/// built dictionary - which is why the check below reads the source instead. Ten pairs had accumulated
/// before anybody looked, four of them with different Polish; a group's roster was headed with the word
/// meant for counting people.
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
        Assert.NotEmpty(PolishTranslations.ByEnglish);
    }

    [Fact]
    public void No_English_string_is_translated_twice()
    {
        var twice = KeysWrittenInTheSource()
            .GroupBy(key => key, StringComparer.Ordinal)
            .Where(entries => entries.Count() > 1)
            .Select(entries => entries.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(twice.Count == 0, $"Translated twice: {string.Join(" | ", twice)}");
    }

    [Fact]
    public void The_source_was_actually_found()
    {
        // Guards the check above: a moved file would otherwise let it pass by reading nothing at all,
        // which is the failure mode that matters for a test that reads its subject off disk.
        Assert.True(KeysWrittenInTheSource().Count > 500);
    }

    /// <summary>
    /// Every key as it is written in the dictionary's own source, duplicates and all - the one place a
    /// key that was overwritten still exists. Read off disk the way Orbit.Mobile.Tests' own translation
    /// sweep reads the markup it checks.
    /// </summary>
    private static IReadOnlyList<string> KeysWrittenInTheSource()
    {
        var source = Path.Combine(
            RepositoryRoot(), "src", "Shared", "Orbit.Localization", "PolishTranslations.cs");

        // The escape alternative matters: several keys carry a quoted phrase of their own.
        return [.. Regex.Matches(File.ReadAllText(source), """^\s*\["((?:[^"\\]|\\.)*)"\]\s*=""", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)];
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Orbit.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find Orbit.sln above the test binaries.");
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
