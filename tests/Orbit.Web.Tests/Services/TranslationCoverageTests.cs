using System.Text.RegularExpressions;
using Orbit.Localization;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Reads Orbit.Web's own markup and code, and checks that everything it asks to be translated actually
/// is - the web's half of the sweep the phone has had for months (see Orbit.Mobile.Tests'
/// TranslationCoverageTests, which this mirrors deliberately rather than sharing code with: each reads a
/// different shape of source, and the shared part is the dictionary they both check against).
///
/// A missing translation is invisible by design - the English shows through, which is what makes it safe
/// to translate a screen at a time - and that is exactly why it needs a test rather than an eye. Without
/// this, the four pages behind the footer sat entirely in English for as long as they existed, on a page
/// somebody reading Polish would have reached from every screen in the app.
/// </summary>
public sealed partial class TranslationCoverageTests
{
    /// <summary>
    /// Reads the same in both languages. Listed rather than inferred, so adding one is a decision
    /// somebody makes on purpose - the same rule the phone's own list follows.
    /// </summary>
    private static readonly HashSet<string> SameInEveryLanguage =
        ["Orbit", "Google", "English", "Polski", "OK"];

    [Fact]
    public void Every_string_the_web_asks_to_translate_has_a_Polish_translation()
    {
        var untranslated = StringsAskedFor()
            .Where(text => !SameInEveryLanguage.Contains(text))
            .Where(text => !PolishTranslations.ByEnglish.ContainsKey(text))
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            untranslated.Count == 0,
            $"No Polish for: {string.Join(" | ", untranslated)}");
    }

    [Fact]
    public void The_source_was_actually_found()
    {
        // Guards the check above: a moved folder would otherwise let it pass by reading nothing at all,
        // which is the failure mode that matters for a test that reads its subject off disk.
        Assert.True(StringsAskedFor().Count > 500);
    }

    /// <summary>
    /// Both ways a page asks for words: <c>T["…"]</c> and <c>T.Format("…", …)</c>. A key split across
    /// concatenated lines is put back together, because the longer sentences do not fit on one.
    /// </summary>
    private static IReadOnlyCollection<string> StringsAskedFor()
    {
        var asked = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in SourceFiles())
        {
            var text = File.ReadAllText(file);
            foreach (var match in LookedUp().Matches(text).Concat(Formatted().Matches(text)))
            {
                asked.Add(string.Concat(Literal().Matches(match.Groups[1].Value)
                    .Select(literal => Unescaped(literal.Groups[1].Value))));
            }
        }

        return asked;
    }

    private static IEnumerable<string> SourceFiles()
        => new[] { "*.razor", "*.cs" }
            .SelectMany(pattern => Directory.EnumerateFiles(
                Path.Combine(RepositoryRoot(), "src", "Clients", "Orbit.Web"), pattern, SearchOption.AllDirectories))
            // Generated code repeats whatever the source already said, and obj/ carries a copy of it.
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

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

    /// <summary>
    /// A key as the running app asks for it rather than as the source writes it: the compiler has
    /// already turned <c>\"</c> into a quote by the time the dictionary is looked up, and a sentence
    /// quoting something - half of the longer ones do - would otherwise never match its own translation.
    /// </summary>
    private static string Unescaped(string literal)
        => literal.Replace("\\\"", "\"").Replace("\\\\", "\\");

    [GeneratedRegex(@"T\[\s*((?:""(?:[^""\\]|\\.)*""\s*\+?\s*)+)\]")]
    private static partial Regex LookedUp();

    [GeneratedRegex(@"T\.Format\(\s*((?:""(?:[^""\\]|\\.)*""\s*\+?\s*)+)[,)]")]
    private static partial Regex Formatted();

    [GeneratedRegex(@"""((?:[^""\\]|\\.)*)""")]
    private static partial Regex Literal();
}
