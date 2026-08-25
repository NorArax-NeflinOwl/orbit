using Orbit.Core.Abstractions;
using Xunit;

namespace Orbit.Api.Tests.Sharing;

/// <summary>
/// Covers the access rules themselves, away from any one kind of item - notes, task lists, events and
/// warehouses all ask these, so getting them wrong here would be wrong in four places at once.
/// </summary>
public sealed class ShareAccessTests
{
    [Theory]
    [InlineData(ShareAccessLevel.ReadOnly, false)]
    [InlineData(ShareAccessLevel.Share, false)]
    [InlineData(ShareAccessLevel.EditOnly, true)]
    [InlineData(ShareAccessLevel.CanEdit, true)]
    public void Only_the_two_editing_levels_permit_a_change(ShareAccessLevel level, bool expected)
        => Assert.Equal(expected, level.AllowsEditing());

    [Theory]
    [InlineData(ShareAccessLevel.ReadOnly)]
    [InlineData(ShareAccessLevel.Share)]
    [InlineData(ShareAccessLevel.EditOnly)]
    [InlineData(ShareAccessLevel.CanEdit)]
    public void A_read_only_holder_hands_out_nothing(ShareAccessLevel requested)
        => Assert.False(ShareAccessLevel.ReadOnly.CanGrant(requested));

    [Theory]
    [InlineData(ShareAccessLevel.ReadOnly, true)]
    [InlineData(ShareAccessLevel.Share, true)]
    [InlineData(ShareAccessLevel.EditOnly, false)]
    [InlineData(ShareAccessLevel.CanEdit, false)]
    public void A_share_holder_passes_on_reading_and_sharing_but_never_editing(ShareAccessLevel requested, bool expected)
        => Assert.Equal(expected, ShareAccessLevel.Share.CanGrant(requested));

    [Theory]
    [InlineData(ShareAccessLevel.ReadOnly, true)]
    [InlineData(ShareAccessLevel.Share, true)]
    [InlineData(ShareAccessLevel.EditOnly, false)]
    [InlineData(ShareAccessLevel.CanEdit, false)]
    public void An_edit_only_holder_can_share_but_never_the_editing(ShareAccessLevel requested, bool expected)
    {
        // The whole point of the level: work on it yourself, don't decide who else gets to. This is also
        // the one rule the rank alone can't express, since EditOnly outranks Share.
        Assert.Equal(expected, ShareAccessLevel.EditOnly.CanGrant(requested));
    }

    [Theory]
    [InlineData(ShareAccessLevel.ReadOnly)]
    [InlineData(ShareAccessLevel.Share)]
    [InlineData(ShareAccessLevel.EditOnly)]
    [InlineData(ShareAccessLevel.CanEdit)]
    public void A_full_holder_can_pass_on_anything(ShareAccessLevel requested)
        => Assert.True(ShareAccessLevel.CanEdit.CanGrant(requested));

    [Fact]
    public void Nobody_grants_more_than_they_hold()
    {
        // Stated once as the general rule, rather than trusting the tables above to have covered it.
        var levels = Enum.GetValues<ShareAccessLevel>();
        foreach (var holder in levels)
        {
            foreach (var requested in levels.Where(level => level > holder))
            {
                Assert.False(holder.CanGrant(requested), $"{holder} must not be able to grant {requested}.");
            }
        }
    }
}
