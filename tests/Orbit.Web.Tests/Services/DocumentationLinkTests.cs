using System.Text.RegularExpressions;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Every link between the documents in <c>info/</c> points at something that is there.
///
/// This is rule 17's problem in a second place: nothing fails when a cross-reference goes stale, and a
/// wrong one is still believed. A link to a section that has been renamed is worse than no link - GitHub
/// renders it as an ordinary link, clicking it lands at the top of the page, and the reader concludes
/// the section they were sent to find does not exist.
///
/// It found two the day it was written, both of the same kind and both years' worth of reading apart
/// from being noticed: a link to "§6" for a section renumbered to 7, and a link to a **bold paragraph**
/// as though bold text made an anchor. It does not; only a heading does, which is the trap this closes.
///
/// Deliberately narrow. It checks *links between documents*, not the code names the documents quote:
/// those name things that were deliberately removed ("`GoToTaskList`, now gone") and things not built
/// yet ("`tests/Orbit.Maui.Tests` does not exist", which is the sentence saying so), and a test that
/// refused either would be one nobody could keep green honestly.
///
/// It lives in this project because this is where the other test that reads a file out of the
/// repository lives - see <see cref="StylesheetTokenTests"/> - and because `dotnet test Orbit.sln` is
/// the only check anything gets before it reaches Coding.
/// </summary>
public sealed partial class DocumentationLinkTests
{
    [Fact]
    public void Every_link_between_the_documents_points_at_a_file_that_exists()
    {
        var broken = EveryLink()
            .Where(link => link.TargetPath.Length > 0 && !File.Exists(link.ResolvedPath))
            .Select(link => $"{link.InDocument} -> {link.Written}")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(broken.Count == 0, $"These links name a document that is not there:{Environment.NewLine}"
            + string.Join(Environment.NewLine, broken));
    }

    /// <summary>
    /// And every "#section" points at a real heading. The half that actually rots: a document is
    /// renamed rarely and noticed at once, while a heading is reworded in the same commit that reworded
    /// the section under it, and every link written to the old wording quietly stops working.
    /// </summary>
    [Fact]
    public void Every_link_to_a_section_points_at_a_heading_that_exists()
    {
        var broken = EveryLink()
            .Where(link => link.Anchor.Length > 0 && File.Exists(link.ResolvedPath))
            .Where(link => !HeadingSlugsIn(link.ResolvedPath).Contains(link.Anchor))
            .Select(link => $"{link.InDocument} -> {link.Written}")
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(broken.Count == 0, $"These links name a section that is not there:{Environment.NewLine}"
            + string.Join(Environment.NewLine, broken));
    }

    /// <summary>
    /// The guard on both, so neither can pass by reading nothing - a path that stopped resolving, a
    /// pattern that stopped matching, and either test goes quietly green over an empty list.
    /// </summary>
    [Fact]
    public void The_documents_are_actually_being_read()
    {
        var links = EveryLink().ToList();

        Assert.True(Documents().Count > 10, $"Only {Documents().Count} documents were found under info/.");
        Assert.True(links.Count > 50, $"Only {links.Count} links were found across them.");
        Assert.Contains(links, link => link.Anchor.Length > 0);
    }

    private static IEnumerable<DocumentLink> EveryLink()
    {
        foreach (var document in Documents())
        {
            var directory = Path.GetDirectoryName(document)!;
            foreach (Match match in LinkPattern().Matches(File.ReadAllText(document)))
            {
                var targetPath = match.Groups["path"].Value;
                var anchor = match.Groups["anchor"].Value;
                yield return new DocumentLink(
                    InDocument: Path.GetRelativePath(RepositoryRoot(), document).Replace('\\', '/'),
                    Written: match.Value[(match.Value.IndexOf('(') + 1)..^1],
                    TargetPath: targetPath,
                    // A bare "#section" means this same document.
                    ResolvedPath: targetPath.Length == 0
                        ? document
                        : Path.GetFullPath(Path.Combine(directory, targetPath)),
                    Anchor: anchor);
            }
        }
    }

    /// <summary>
    /// How GitHub makes an anchor out of a heading: lower-cased, everything but letters, digits, spaces
    /// and hyphens dropped, then spaces to hyphens. That is why an em dash between two words leaves a
    /// double hyphen behind - it goes, and the two spaces around it each become one.
    /// </summary>
    private static HashSet<string> HeadingSlugsIn(string document)
        => [.. File.ReadLines(document)
            .Where(line => HeadingPattern().IsMatch(line))
            .Select(line => HeadingPattern().Replace(line, string.Empty).Trim())
            .Select(heading => new string([.. heading.ToLowerInvariant()
                .Where(character => char.IsAsciiLetterOrDigit(character) || character is ' ' or '-')])
                .Replace(' ', '-'))];

    private static IReadOnlyList<string> Documents()
        => [.. Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "info"), "*.md", SearchOption.AllDirectories)];

    /// <summary>The tests run from bin/, so the repository is found by walking up to the solution.</summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Orbit.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find Orbit.sln above the test binaries.");
    }

    /// <param name="Written">The link as it appears, which is what a failure has to print to be actionable.</param>
    private sealed record DocumentLink(
        string InDocument, string Written, string TargetPath, string ResolvedPath, string Anchor);

    /// <summary>
    /// A markdown link to another document, to a section of one, or to a section of this one. Only
    /// links to .md files: an http one is somebody else's to keep working, and a link to a source file
    /// is checked by nothing here for the reason in the class comment.
    /// </summary>
    [GeneratedRegex(@"\]\((?<path>[A-Za-z0-9_./-]*\.md)?(?:#(?<anchor>[A-Za-z0-9-]+))?\)")]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"^#{1,6}\s+")]
    private static partial Regex HeadingPattern();
}
