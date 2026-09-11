using System.Text.RegularExpressions;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// That a sentence saying what a screen or a section is for sits behind a "?" beside its name, and is
/// not also drawn under it.
///
/// Folded on 2026-09-11, the way Orbit.Web folded its own the same day. Nothing fails when one comes back
/// out: a Label under the name compiles, translates and reads perfectly well, which is exactly how the
/// phone kept these after the browser had stopped. And the sentences themselves are not deleted - each
/// is still the hint behind its mark - so this also pins that none was lost on the way.
///
/// Read from the markup because that is where the choice is made; the view models never held these.
/// </summary>
public sealed partial class FoldedDescriptionTests
{
    /// <summary>
    /// Where a sentence may live now: beside the screen's name in the bar, or beside a heading or a
    /// field's name that the screen draws itself.
    /// </summary>
    private static readonly HashSet<string> Folds =
        ["controls:NavigationBar", "controls:NavigationBar.Description", "controls:FieldHint"];

    public static TheoryData<string, string> Folded => new()
    {
        { "ContactInfoPage.xaml", "Who this is, and how to reach them." },
        { "GroupsPage.xaml", "Conversations with more than one other person." },
        { "UpdatePage.xaml", "Where a newer Orbit comes from, and how to install it." },
        { "CopyHistoryPage.xaml", "Copies of this, and what each came from." },
        { "CopyReviewPage.xaml", "What you wrote while you were offline, beside what it came from." },
        { "DiagnosticsPage.xaml", "Orbit's own log, on this phone. Nothing leaves it unless you send it." },
        { "RegisterPage.xaml", "One account for the browser and this phone." },
        { "PasswordResetPage.xaml", "Enter the account, and Orbit emails a code to the address it was registered with." },
        { "ShelfProductFields.xaml", "Always on the restock list, however much there is" },
    };

    [Theory]
    [MemberData(nameof(Folded))]
    public void The_sentence_is_behind_a_mark_and_said_nowhere_else_on_the_screen(string file, string sentence)
    {
        var markup = Read(file);

        var at = markup.IndexOf(sentence, StringComparison.Ordinal);
        Assert.True(at >= 0, $"{file} no longer carries \"{sentence}\" at all.");
        Assert.Equal(at, markup.LastIndexOf(sentence, StringComparison.Ordinal));
        Assert.Contains(HolderOf(markup, at), Folds);
    }

    /// <summary>
    /// The element a piece of text belongs to: the nearest tag opened before it, looking past the
    /// Translate that only asks for the words.
    /// </summary>
    private static string HolderOf(string markup, int at)
        => OpenedTag().Matches(markup[..at])
            .Select(match => match.Groups[1].Value)
            .Last(name => name != "controls:Translate");

    private static string Read(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var found = Directory.EnumerateFiles(
                Path.Combine(directory!.FullName, "src", "Clients", "Orbit.Maui"), file, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();

        Assert.Single(found);
        return File.ReadAllText(found[0]).Replace("&apos;", "'").Replace("&quot;", "\"");
    }

    [GeneratedRegex(@"<([\w:.]+)")]
    private static partial Regex OpenedTag();
}
