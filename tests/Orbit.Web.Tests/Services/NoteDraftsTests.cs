using Orbit.Contracts.Notes;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// What is written in a note and not saved, kept while the tab lives - see NoteDrafts. What matters
/// here is what counts as unsaved: a note left exactly as it was stored keeps nothing, so leaving the
/// editor after reading one does not warn about it.
/// </summary>
public sealed class NoteDraftsTests
{
    private static readonly Guid NoteId = Guid.NewGuid();

    private static NoteDrafts.Draft Draft(string name, params string[] lines)
        => new(
            name,
            [.. lines.Select(line => new NoteContentLineDto(line, false, false))],
            IsPrivate: false,
            Priority: "Normal",
            FolderId: null,
            Tags: []);

    [Fact]
    public void Writing_that_says_something_new_is_kept_and_named()
    {
        var drafts = new NoteDrafts();

        drafts.Keep(NoteId, Draft("Shopping", "Shopping", "milk"), Draft("Shopping", "Shopping"));

        Assert.True(drafts.HasAny);
        Assert.Equal(["Shopping"], drafts.Names);
        Assert.Equal(["Shopping", "milk"], drafts.For(NoteId)!.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// A note read and left alone is not unsaved writing. Compared by what it says rather than field by
    /// field, because a line carries lists of its own and two records holding equal lists are not equal.
    /// </summary>
    [Fact]
    public void A_note_left_as_it_was_stored_keeps_nothing()
    {
        var drafts = new NoteDrafts();

        drafts.Keep(NoteId, Draft("Shopping", "Shopping", "milk"), Draft("Shopping", "Shopping", "milk"));

        Assert.False(drafts.HasAny);
        Assert.Null(drafts.For(NoteId));
    }

    /// <summary>And writing put back where it came from drops what was being kept for it.</summary>
    [Fact]
    public void Writing_taken_back_to_what_was_stored_is_no_longer_kept()
    {
        var drafts = new NoteDrafts();
        drafts.Keep(NoteId, Draft("Shopping", "Shopping", "milk"), Draft("Shopping", "Shopping"));

        drafts.Keep(NoteId, Draft("Shopping", "Shopping"), Draft("Shopping", "Shopping"));

        Assert.False(drafts.HasAny);
    }

    [Fact]
    public void Saving_one_note_leaves_the_others_alone()
    {
        var drafts = new NoteDrafts();
        var other = Guid.NewGuid();
        drafts.Keep(NoteId, Draft("Shopping", "milk"), Draft("Shopping"));
        drafts.Keep(other, Draft("Packing", "socks"), Draft("Packing"));

        drafts.Forget(NoteId);

        Assert.Equal(["Packing"], drafts.Names);
    }

    /// <summary>Leaving anyway, after the warning: everything goes rather than waiting for a return nobody made.</summary>
    [Fact]
    public void Leaving_after_the_warning_throws_it_all_away()
    {
        var drafts = new NoteDrafts();
        drafts.Keep(NoteId, Draft("Shopping", "milk"), Draft("Shopping"));

        drafts.ForgetEverything();

        Assert.False(drafts.HasAny);
    }
}
