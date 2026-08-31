using Orbit.Mobile.Screens.Copies;
using Xunit;

namespace Orbit.Mobile.Tests.Screens.Copies;

/// <summary>
/// What the review window shows between two versions of the same thing. Small on its own, but it is the
/// whole basis on which somebody decides which version to keep - a diff that overstates the change makes
/// every review look like a conflict, and one that understates it loses somebody's work quietly.
/// </summary>
public sealed class VersionDiffTests
{
    [Fact]
    public void Two_versions_that_say_the_same_thing_show_nothing_changed()
    {
        var before = Lines("milk", "bread");

        var diff = VersionDiff.Between(before, Lines("milk", "bread"));

        Assert.All(diff, line => Assert.Equal(LineChange.Unchanged, line.Change));
        Assert.False(VersionDiff.Differ(before, Lines("milk", "bread")));
    }

    [Fact]
    public void An_added_line_is_marked_and_nothing_else_is()
    {
        var diff = VersionDiff.Between(Lines("milk"), Lines("milk", "bread"));

        Assert.Equal([("milk", LineChange.Unchanged), ("bread", LineChange.Added)], Described(diff));
    }

    /// <summary>
    /// The reason the diff does the longest common subsequence rather than compare index by index: an
    /// insertion at the top shifts every line after it, and the naive answer was "everything changed".
    /// </summary>
    [Fact]
    public void A_line_inserted_at_the_top_leaves_the_rest_alone()
    {
        var diff = VersionDiff.Between(Lines("milk", "bread"), Lines("eggs", "milk", "bread"));

        Assert.Equal(
            [("eggs", LineChange.Added), ("milk", LineChange.Unchanged), ("bread", LineChange.Unchanged)],
            Described(diff));
    }

    [Fact]
    public void A_removed_line_is_shown_as_removed_rather_than_left_out()
    {
        // Left out, a review would be asked to approve a deletion it never saw.
        var diff = VersionDiff.Between(Lines("milk", "bread"), Lines("bread"));

        Assert.Equal([("milk", LineChange.Removed), ("bread", LineChange.Unchanged)], Described(diff));
    }

    /// <summary>
    /// Whatever a repository writes into a line is what the diff compares, ticks and dates included -
    /// see LocalNoteRepository.Describe and the three beside it.
    /// </summary>
    [Fact]
    public void A_line_that_differs_only_in_its_marking_still_counts_as_changed()
    {
        Assert.True(VersionDiff.Differ(Lines("[ ] milk"), Lines("[x] milk")));
    }

    [Fact]
    public void An_edited_line_reads_as_the_old_one_out_and_the_new_one_in()
    {
        var diff = VersionDiff.Between(Lines("milk"), Lines("oat milk"));

        Assert.Equal([("milk", LineChange.Removed), ("oat milk", LineChange.Added)], Described(diff));
    }

    private static IReadOnlyList<string> Lines(params string[] texts) => texts;

    private static IReadOnlyList<(string, LineChange)> Described(IReadOnlyList<DiffLine> diff)
        => [.. diff.Select(line => (line.Text, line.Change))];
}
