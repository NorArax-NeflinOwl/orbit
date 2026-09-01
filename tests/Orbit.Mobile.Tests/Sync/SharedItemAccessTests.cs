using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// What the level something was shared at lets this phone do with it - the question the app was not
/// asking.
///
/// The failure it stands for was found by opening a note claimed from a public link, which the server
/// grants ReadOnly and never anything more. It opened as an ordinary editable screen: the edit was
/// applied locally, queued, refused by the server with a 403, and given up on some minutes later. The
/// work disappeared, and nothing on the way said why. Orbit.Web has disabled the form for a read-only
/// share all along.
/// </summary>
public sealed class SharedItemAccessTests
{
    [Fact]
    public void Something_of_your_own_carries_no_restriction()
    {
        // Not shared at all, whatever the stored level says - an item nobody offered has no share to
        // read a level off.
        Assert.True(SharedItemAccess.AllowsEditing(new LocalNote { AccessLevel = "ReadOnly" }));
    }

    [Fact]
    public void Something_shared_to_read_cannot_be_edited()
    {
        Assert.False(SharedItemAccess.AllowsEditing(new LocalNote { IsShared = true, AccessLevel = "ReadOnly" }));
    }

    /// <summary>
    /// The trap in writing this as "== CanEdit": EditOnly permits editing too, and a check by equality
    /// quietly calls an editor a reader. It is why the rule is asked of Orbit.Core rather than restated.
    /// </summary>
    [Theory]
    [InlineData("CanEdit")]
    [InlineData("EditOnly")]
    public void Something_shared_for_editing_can_be_edited(string level)
    {
        Assert.True(SharedItemAccess.AllowsEditing(new LocalNote { IsShared = true, AccessLevel = level }));
    }

    /// <summary>Share permits re-sharing and nothing else - see ShareAccessLevel.</summary>
    [Fact]
    public void Something_shared_only_to_pass_on_still_cannot_be_edited()
    {
        Assert.False(SharedItemAccess.AllowsEditing(new LocalNote { IsShared = true, AccessLevel = "Share" }));
    }

    /// <summary>
    /// A level added after this build is one it does not understand, and the safe reading of that is the
    /// narrowest one - the same choice the browser makes.
    /// </summary>
    [Fact]
    public void A_level_this_build_does_not_know_reads_as_read_only()
    {
        Assert.False(SharedItemAccess.AllowsEditing(new LocalNote { IsShared = true, AccessLevel = "SomethingNewer" }));
    }

    [Fact]
    public void Every_kind_that_can_be_shared_answers_the_same_question()
    {
        // One rule for four types, which is the reason it takes ISharedState rather than a note.
        Assert.False(SharedItemAccess.AllowsEditing(new LocalTaskList { IsShared = true, AccessLevel = "ReadOnly" }));
        Assert.False(SharedItemAccess.AllowsEditing(new LocalCalendarEvent { IsShared = true, AccessLevel = "ReadOnly" }));
        Assert.False(SharedItemAccess.AllowsEditing(new LocalWarehouse { IsShared = true, AccessLevel = "ReadOnly" }));
    }

    /// <summary>
    /// Said differently from the offline refusals on purpose: those pass when the phone reconnects, and
    /// this one never does. Telling somebody to try again online would send them to wait for nothing.
    /// </summary>
    [Fact]
    public void It_is_explained_as_something_no_connection_will_fix()
    {
        var translations = new Translations(new InMemoryLanguageStore());
        var sharedToRead = new LocalNote { IsShared = true, AccessLevel = "ReadOnly" };

        Assert.DoesNotContain("online", SharedItemAccess.WhyItCannotBeEdited(sharedToRead, translations));
        Assert.Contains("Ask whoever shared it", SharedItemAccess.WhyItCannotBeEdited(sharedToRead, translations));
        Assert.Empty(SharedItemAccess.WhyItCannotBeEdited(new LocalNote(), translations));
    }
}
