using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// The restrictive policy from info/orbit-maui-plan.md §5.4. The decision it encodes is not obvious, so
/// it is worth restating: sharing in Orbit is not a copy - two people with CanEdit are editing one row,
/// which is why the server holds edit locks. A phone cannot hold a lock, so it can only find out at
/// replay time that someone else was editing, long after the user did the work. Refusing up front is
/// the honest option.
/// </summary>
public sealed class OfflineEditPolicyTests
{
    [Fact]
    public void Offline_a_note_nobody_else_can_touch_is_editable()
    {
        var refusal = OfflineEditPolicy.Evaluate(new LocalNote(), Offline);

        Assert.Equal(OfflineEditRefusal.None, refusal);
    }

    [Fact]
    public void Offline_a_note_somebody_shared_with_you_is_not_editable()
    {
        var refusal = OfflineEditPolicy.Evaluate(new LocalNote { IsShared = true }, Offline);

        Assert.Equal(OfflineEditRefusal.SharedWithYou, refusal);
    }

    [Fact]
    public void Offline_a_note_you_shared_out_is_not_editable_either()
    {
        // The owner's side, and the case the API could not answer until IsSharedWithOthers existed -
        // without it this note is indistinguishable from a private one.
        var refusal = OfflineEditPolicy.Evaluate(new LocalNote { IsSharedWithOthers = true }, Offline);

        Assert.Equal(OfflineEditRefusal.SharedWithOthers, refusal);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Online_the_server_decides_and_this_policy_refuses_nothing(bool isShared, bool isSharedWithOthers)
    {
        var note = new LocalNote { IsShared = isShared, IsSharedWithOthers = isSharedWithOthers };

        // Online the app can take a real edit lock, which is a better answer than guessing.
        Assert.True(OfflineEditPolicy.IsAllowed(note, Online));
    }

    private static INetworkStatus Offline => new FixedNetworkStatus(false);

    private static INetworkStatus Online => new FixedNetworkStatus(true);

    private sealed record FixedNetworkStatus(bool IsOnline) : INetworkStatus;
}
