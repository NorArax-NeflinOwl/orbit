using System.Text.RegularExpressions;
using Orbit.Localization;
using Xunit;

namespace Orbit.Mobile.Tests.Localization;

/// <summary>
/// Reads Orbit.Maui's own XAML and checks that everything it asks to be translated actually is.
///
/// A missing translation is invisible by design - the English shows through - which is exactly why it
/// needs a test. Without this, a screen added in six months is half Polish and nobody notices until
/// somebody reading Polish opens it.
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
        var untranslated = StringsUsedInMarkup()
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

    private static IReadOnlyCollection<string> StringsUsedInMarkup()
    {
        var markup = Path.Combine(RepositoryRoot(), "src", "Clients", "Orbit.Maui");
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(markup, "*.xaml", SearchOption.AllDirectories))
        {
            foreach (Match match in TranslatedString().Matches(File.ReadAllText(file)))
            {
                used.Add(match.Groups[1].Value);
            }
        }

        return used;
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

    [GeneratedRegex(@"\{controls:Translate '([^']+)'\}")]
    private static partial Regex TranslatedString();
}
