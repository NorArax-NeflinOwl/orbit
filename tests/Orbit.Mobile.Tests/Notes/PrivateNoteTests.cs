using Orbit.Mobile.Data;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Tests.Notes;

/// <summary>
/// What a private note shows on a list while private things are locked. The row is deliberately still
/// there: a note that vanished would read as deleted, which is a worse lie than one that says nothing.
/// </summary>
public sealed class PrivateNoteTests
{
    [Fact]
    public void A_private_note_says_nothing_about_itself_while_locked()
    {
        var row = Describe(new LocalNote { Title = "Bank details", IsPrivate = true }, privateItemsAreUnlocked: false);

        Assert.True(row.IsHidden);
        Assert.Equal("Private", row.DisplayTitle);
        Assert.False(row.CanBeOpened);
    }

    [Fact]
    public void The_same_note_reads_normally_once_unlocked()
    {
        var row = Describe(new LocalNote { Title = "Bank details", IsPrivate = true }, privateItemsAreUnlocked: true);

        Assert.False(row.IsHidden);
        Assert.Equal("Bank details", row.DisplayTitle);
        Assert.True(row.CanBeOpened);
    }

    [Fact]
    public void An_ordinary_note_is_never_hidden()
    {
        // The gate is only about IsPrivate; locking everything would make the app unusable rather than
        // private.
        var row = Describe(new LocalNote { Title = "Shopping" }, privateItemsAreUnlocked: false);

        Assert.False(row.IsHidden);
        Assert.Equal("Shopping", row.DisplayTitle);
    }

    /// <summary>
    /// A note shared with this reader offers a pin like any other. It used to be left out, because the
    /// server refused anybody but the owner - a recipient's answer goes on their own grant now
    /// (NoteShare.IsPinnedByRecipient), so there is something for the control to write. The same change
    /// on the task list side is TaskListRowTests.
    /// </summary>
    [Fact]
    public void A_note_shared_with_this_reader_offers_a_pin_of_its_own()
    {
        var row = Describe(new LocalNote { Title = "Shopping", IsShared = true }, privateItemsAreUnlocked: true);

        Assert.True(row.CanBePinned);
    }

    /// <summary>And a locked row still offers nothing at all, which is the one thing the gate is left for.</summary>
    [Fact]
    public void A_private_note_nobody_has_unlocked_offers_no_pin()
    {
        var row = Describe(new LocalNote { Title = "Bank details", IsPrivate = true }, privateItemsAreUnlocked: false);

        Assert.False(row.CanBePinned);
    }

    private static NoteListItem Describe(LocalNote note, bool privateItemsAreUnlocked)
        => NoteListItem.From(
            note, hasUnsentChanges: false, FixedNetworkStatus.Online, privateItemsAreUnlocked,
            new Translations(new InMemoryLanguageStore()), DateTimeOffset.UtcNow);
}
