using Orbit.Core.Folders;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Which tab a card is read under. One rule for four pages, and the only place the order of the
/// built-in folders is decided - see FolderPlacement.
/// </summary>
public sealed class FolderPlacementTests
{
    private static readonly Guid WorkFolderId = Guid.NewGuid();
    private static readonly Guid[] Known = [WorkFolderId];

    [Fact]
    public void Something_nobody_filed_is_in_Public()
        => Assert.Equal(
            FolderKey.Of(BuiltInFolder.Public),
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
    /// Finished wins over everything, which is what "all finished lists gather there" has to mean - a
    /// folder is asked what is still to do, and a list with nothing left on it is not an answer to that.
    /// </summary>
    [Fact]
    public void A_finished_list_is_in_Finished_even_when_it_was_filed_somewhere()
        => Assert.Equal(
            FolderKey.Of(BuiltInFolder.Finished),
            FolderPlacement.Of(WorkFolderId, isPrivate: false, isFinished: true, Known));

    /// <summary>
    /// A folder deleted on another device leaves cards pointing at an id nothing knows. They fall back
    /// to a built-in folder rather than vanishing from every tab, which is what a stale id would do.
    /// </summary>
    [Fact]
    public void Something_filed_under_a_folder_that_is_gone_falls_back_to_a_built_in_one()
        => Assert.Equal(
            FolderKey.Of(BuiltInFolder.Public),
            FolderPlacement.Of(Guid.NewGuid(), isPrivate: false, isFinished: false, Known));
}
