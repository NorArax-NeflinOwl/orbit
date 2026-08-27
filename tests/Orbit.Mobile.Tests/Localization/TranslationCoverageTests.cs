using System.Text.RegularExpressions;
using Orbit.Localization;
using Xunit;

namespace Orbit.Mobile.Tests.Localization;

/// <summary>
/// Reads Orbit.Maui's XAML <b>and</b> Orbit.Mobile's own code, and checks that everything either of them
/// asks to be translated actually is.
///
/// A missing translation is invisible by design - the English shows through - which is exactly why it
/// needs a test. Without this, a screen added in six months is half Polish and nobody notices until
/// somebody reading Polish opens it. The code half was added after the markup half had passed for weeks
/// while every status line, refusal and empty-list message on the phone was still in English: the sweep
/// could not see them, so it could not miss them.
/// </summary>
public sealed partial class TranslationCoverageTests
{
    /// <summary>
    /// A product's name and a bare glyph read the same in both languages. Listed rather than inferred,
    /// so adding one is a decision somebody makes on purpose.
    /// </summary>
    private static readonly HashSet<string> SameInEveryLanguage = ["Orbit", "+", "−", "✕", "English", "Polski"];

    [Fact]
    public void Every_string_the_app_asks_to_translate_has_a_Polish_translation()
    {
        var untranslated = StringsUsedInMarkup().Concat(StringsUsedInCode())
            .Where(text => !SameInEveryLanguage.Contains(text))
            .Where(text => !PolishTranslations.ByEnglish.ContainsKey(text))
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            untranslated.Count == 0,
            $"No Polish for: {string.Join(" | ", untranslated)}");
    }

    [Fact]
    public void The_markup_was_actually_found()
    {
        // Guards the test itself: a moved folder would otherwise make the check above pass by finding
        // nothing at all, which is the failure mode that matters for a test like this.
        Assert.True(StringsUsedInMarkup().Count > 50);
    }

    [Fact]
    public void The_code_was_actually_found()
    {
        Assert.True(StringsUsedInCode().Count > 50);
    }

    /// <summary>
    /// The check above only covers what the markup <i>asks</i> to translate, so a label written straight
    /// into the page passed it by being invisible to it. Seven of them were, including the whole
    /// explanation on the chat-key screen. This is the other half: text a page states outright.
    /// </summary>
    [Fact]
    public void No_page_writes_its_own_text_instead_of_asking_for_it()
    {
        var stated = StringsStatedInMarkup()
            .Where(text => !SameInEveryLanguage.Contains(text))
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            stated.Count == 0,
            $"Written into the markup rather than translated: {string.Join(" | ", stated)}");
    }

    private static IReadOnlyCollection<string> StringsUsedInMarkup()
    {
        var markup = Path.Combine(RepositoryRoot(), "src", "Clients", "Orbit.Maui");
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(markup, "*.xaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var match in TranslatedString().Matches(text).Concat(TranslatedElement().Matches(text)))
            {
                used.Add(Unescaped(match.Groups[1].Value));
            }
        }

        return used;
    }

    /// <summary>
    /// What the view models ask the dictionary for. Both forms are matched, and both allow a key split
    /// across concatenated lines, because the longer sentences do not fit on one.
    /// </summary>
    private static IReadOnlyCollection<string> StringsUsedInCode()
    {
        var code = Path.Combine(RepositoryRoot(), "src", "Clients", "Orbit.Mobile");
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(code, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var match in LookedUp().Matches(text).Concat(Formatted().Matches(text)))
            {
                used.Add(string.Concat(Literal().Matches(match.Groups[1].Value)
                    .Select(literal => literal.Groups[1].Value.Replace("\\\"", "\""))));
            }
        }

        return used;
    }

    /// <summary>
    /// Text a page states rather than asks for. A value that is a binding or any other markup extension
    /// is not stated text, and neither is a separator or a single glyph - those carry no language.
    /// </summary>
    private static IReadOnlyCollection<string> StringsStatedInMarkup()
    {
        var markup = Path.Combine(RepositoryRoot(), "src", "Clients", "Orbit.Maui");
        var stated = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(markup, "*.xaml", SearchOption.AllDirectories))
        {
            foreach (Match match in ShownText().Matches(File.ReadAllText(file)))
            {
                var text = Unescaped(match.Groups[2].Value);
                if (!text.StartsWith('{') && text.Trim().Length > 1)
                {
                    stated.Add(text);
                }
            }
        }

        return stated;
    }

    /// <summary>The tests run from bin/, so the repository is found by walking up to the solution.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Orbit.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find Orbit.sln above the test binaries.");
    }

    /// <summary>XML entities have to come back out, or the key does not match the dictionary's.</summary>
    private static string Unescaped(string text)
        => text.Replace("&quot;", "\"").Replace("&apos;", "\'").Replace("&lt;", "<")
            .Replace("&gt;", ">").Replace("&amp;", "&");

    [GeneratedRegex(@"\{controls:Translate '([^']+)'\}")]
    private static partial Regex TranslatedString();

    /// <summary>
    /// The property-element form, <c>&lt;controls:Translate Text="…" /&gt;</c>. It exists because a
    /// markup extension's single-quoted argument cannot carry an apostrophe, and several of the longer
    /// sentences have one.
    /// </summary>
    [GeneratedRegex("<controls:Translate Text=\"([^\"]+)\"")]
    private static partial Regex TranslatedElement();

    /// <summary>The three attributes that put words on screen. Anything else is not read as language.</summary>
    [GeneratedRegex("(?<!controls:Translate )(Text|Placeholder|Title)=\"([^\"]*)\"")]
    private static partial Regex ShownText();

    [GeneratedRegex(@"translations\[\s*((?:""(?:[^""\\]|\\.)*""\s*\+?\s*)+)\]")]
    private static partial Regex LookedUp();

    [GeneratedRegex(@"translations\.Format\(\s*((?:""(?:[^""\\]|\\.)*""\s*\+?\s*)+)[,)]")]
    private static partial Regex Formatted();

    [GeneratedRegex(@"""((?:[^""\\]|\\.)*)""")]
    private static partial Regex Literal();
}
