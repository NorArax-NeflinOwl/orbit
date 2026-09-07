using System.Text.RegularExpressions;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Reads app.css and checks that every colour, font and size it asks for is one the stylesheet actually
/// defines.
///
/// A CSS variable that does not exist fails **silently**: the declaration is dropped and the property
/// keeps whatever it had, which for a background means none at all. That is not a theoretical worry -
/// the panel of words a category is chosen from asked for a `--surface` this stylesheet has never
/// defined, so it drew with no background and the page showed straight through it, on every screen
/// with a categories box. Nothing failed, nothing logged, and only somebody looking at it could tell.
/// </summary>
public sealed partial class StylesheetTokenTests
{
    /// <summary>
    /// The variables a component sets on the element itself, in a style attribute, rather than the
    /// stylesheet declaring them - see ItemCard's --item-card-accent and the checklist's indent. Listed
    /// rather than inferred: each one is a decision that the value comes from C#, and a new name
    /// appearing here should be somebody's choice rather than a test quietly widening.
    /// </summary>
    private static readonly HashSet<string> SetByAComponent =
        ["--item-card-accent", "--checklist-depth"];

    [Fact]
    public void Every_variable_the_stylesheet_uses_is_one_it_defines()
    {
        var stylesheet = File.ReadAllText(StylesheetPath());

        var defined = DefinitionPattern().Matches(stylesheet).Select(match => match.Groups[1].Value).ToHashSet();
        var undefined = UsagePattern().Matches(stylesheet)
            .Where(match => match.Groups[2].Value.Length == 0)
            .Select(match => match.Groups[1].Value)
            .Where(name => !defined.Contains(name) && !SetByAComponent.Contains(name))
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            undefined.Count == 0,
            $"app.css uses variables nothing defines: {string.Join(", ", undefined)}. "
                + "A missing one drops the whole declaration - a background simply does not paint.");
    }

    /// <summary>
    /// Guards the check above: a moved or renamed stylesheet would otherwise let it pass by reading
    /// nothing at all, which is the failure mode that matters for a test that reads its subject off disk.
    /// </summary>
    [Fact]
    public void The_stylesheet_was_actually_found()
    {
        var stylesheet = File.ReadAllText(StylesheetPath());

        Assert.Contains("--accent:", stylesheet);
        Assert.True(stylesheet.Length > 10_000, "app.css is far shorter than it should be - was the right file read?");
    }

    private static string StylesheetPath()
        => Path.Combine(RepositoryRoot(), "src", "Clients", "Orbit.Web", "wwwroot", "css", "app.css");

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

    /// <summary>A declaration: the name at the start of a line, before its colon.</summary>
    [GeneratedRegex(@"(--[a-zA-Z0-9-]+)\s*:")]
    private static partial Regex DefinitionPattern();

    /// <summary>
    /// A use, and whatever follows the name - a comma means a fallback was given, which is a value even
    /// when nothing defines the variable.
    /// </summary>
    [GeneratedRegex(@"var\((--[a-zA-Z0-9-]+)\s*(,?)")]
    private static partial Regex UsagePattern();
}
