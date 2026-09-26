using Orbit.Core.Folders;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Which tab a card is read under. One rule for every page made of cards - the browser's and the
/// phone's, since FolderPlacement moved into Orbit.Core - and the only place the order of the built-in
/// folders is decided.
/// </summary>
public sealed class FolderPlacementTests
{
    private static readonly Guid WorkFolderId = Guid.NewGuid();
    private static readonly Guid[] Known = [WorkFolderId];

    /// <summary>
    /// Archived beats everything, a folder somebody made included - putting something away is a decision
    /// about whether it is in front of the reader at all, and an archived note still under "Work" would
    /// not have been put anywhere. See BuiltInFolder, which is where the order is decided.
    /// </summary>
    [Fact]
    public void Something_put_away_is_in_Archived_whatever_else_is_true_of_it()
    {
        Assert.Equal(
            FolderKey.Of(BuiltInFolder.Archived),
            FolderPlacement.Of(WorkFolderId, isPrivate: false, isFinished: false, Known, isArchived: true));
        Assert.Equal(
            FolderKey.Of(BuiltInFolder.Archived),
            FolderPlacement.Of(folderId: null, isPrivate: true, isFinished: true, Known, isArchived: true));
    }

    /// <summary>
    /// And the folder it was filed under is still the folder it is filed under: bringing it back puts it
    /// where it was, rather than somewhere a rule had to choose - see Note.Archive, which leaves the id
    /// alone on purpose.
    /// </summary>
    [Fact]
    public void Bringing_it_back_puts_it_under_the_folder_it_was_under()
        => Assert.Equal(
            FolderKey.Of(WorkFolderId),
            FolderPlacement.Of(WorkFolderId, isPrivate: false, isFinished: false, Known, isArchived: false));

    [Fact]
    public void Something_nobody_filed_is_in_All()
        => Assert.Equal(
            FolderKey.Of(BuiltInFolder.All),
            FolderPlacement.Of(folderId: null, isPrivate: false, isFinished: false, Known));

    [Fact]
    public void Something_sealed_and_filed_nowhere_is_in_Private()
        => Assert.Equal(
            FolderKey.Of(BuiltInFolder.Private),
            FolderPlacement.Of(folderId: null, isPrivate: true, isFinished: false, Known));

    /// <summary>
    /// Filing something is not the same decision as sealing it: a private note put in a folder is in
    /// that folder, and Private is only where the ones nobody filed end up.
    /// </summary>
    [Fact]
    public void Something_sealed_and_filed_somewhere_is_in_that_folder()
        => Assert.Equal(
            FolderKey.Of(WorkFolderId),
            FolderPlacement.Of(WorkFolderId, isPrivate: true, isFinished: false, Known));

    /// <summary>
    /// Filing beats finishing. A list somebody put in "Renovation" used to leave that tab the moment its
    /// last entry was ticked off, which reads as the list having been lost: where something goes is a
    /// decision its owner made, and finishing the work is not a decision to file it somewhere else.
    /// </summary>
    [Fact]
    public void A_finished_list_stays_in_the_folder_it_was_filed_under()
        => Assert.Equal(
            FolderKey.Of(WorkFolderId),
            FolderPlacement.Of(WorkFolderId, isPrivate: false, isFinished: true, Known));

    /// <summary>Which leaves Finished doing what it is for: gathering the ones nobody filed anywhere.</summary>
    [Fact]
    public void A_finished_list_nobody_filed_is_in_Finished()
        => Assert.Equal(
            FolderKey.Of(BuiltInFolder.Finished),
            FolderPlacement.Of(folderId: null, isPrivate: false, isFinished: true, Known));

    /// <summary>
    /// A page with no Finished tab passes false and never files anything there - see
    /// FolderPages.HasAFinishedTab. Without that, a finished list on the notes or the dashboard would be
    /// under a tab those pages do not draw, which is the same as not being anywhere.
    /// </summary>
    [Fact]
    public void A_finished_list_is_placed_like_any_other_where_there_is_no_Finished_tab()
        => Assert.Equal(
            FolderKey.Of(BuiltInFolder.All),
            FolderPlacement.Of(folderId: null, isPrivate: false, isFinished: false, Known));

    /// <summary>
    /// A folder deleted on another device leaves cards pointing at an id nothing knows. They fall back
    /// to a built-in folder rather than vanishing from every tab, which is what a stale id would do.
    /// </summary>
    [Fact]
    public void Something_filed_under_a_folder_that_is_gone_falls_back_to_a_built_in_one()
        => Assert.Equal(
            FolderKey.Of(BuiltInFolder.All),
            FolderPlacement.Of(Guid.NewGuid(), isPrivate: false, isFinished: false, Known));

    /// <summary>
    /// All is the one tab wider than the folder of its own name: everything from every folder except what
    /// is sealed and what has been put away, which is the user's decision of 2026-09-24 and what makes
    /// "show me the lot" possible at all. See FolderKey.Holds.
    /// </summary>
    [Theory]
    [InlineData(BuiltInFolder.All)]
    [InlineData(BuiltInFolder.Finished)]
    public void All_holds_what_is_filed_anywhere_and_what_is_finished(BuiltInFolder placement)
    {
        Assert.True(FolderKey.Of(BuiltInFolder.All).Holds(FolderKey.Of(placement)));
        Assert.True(FolderKey.Of(BuiltInFolder.All).Holds(FolderKey.Of(WorkFolderId)));
    }

    /// <summary>
    /// And those are the two it is defined against: a sealed thing is behind the Private tab, and one put
    /// away is in the archive, neither of which "everything you have" is meant to open.
    /// </summary>
    [Theory]
    [InlineData(BuiltInFolder.Private)]
    [InlineData(BuiltInFolder.Archived)]
    public void All_holds_neither_what_is_sealed_nor_what_is_put_away(BuiltInFolder placement)
        => Assert.False(FolderKey.Of(BuiltInFolder.All).Holds(FolderKey.Of(placement)));

    /// <summary>
    /// Every other tab means exactly itself. A folder somebody made is where they put something rather
    /// than a way of gathering it, so widening All changed nothing about the rest of the row.
    /// </summary>
    [Fact]
    public void Every_other_tab_holds_only_what_is_placed_in_it()
    {
        Assert.True(FolderKey.Of(WorkFolderId).Holds(FolderKey.Of(WorkFolderId)));
        Assert.False(FolderKey.Of(WorkFolderId).Holds(FolderKey.Of(BuiltInFolder.All)));
        Assert.False(FolderKey.Of(WorkFolderId).Holds(FolderKey.Of(Guid.NewGuid())));
        Assert.False(FolderKey.Of(BuiltInFolder.Private).Holds(FolderKey.Of(WorkFolderId)));
        Assert.False(FolderKey.Of(BuiltInFolder.Finished).Holds(FolderKey.Of(BuiltInFolder.All)));
        Assert.True(FolderKey.Of(BuiltInFolder.Archived).Holds(FolderKey.Of(BuiltInFolder.Archived)));
    }
}
