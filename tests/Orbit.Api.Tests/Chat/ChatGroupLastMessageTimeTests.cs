using Orbit.Core.Chat.Groups;
using Xunit;

namespace Orbit.Api.Tests.Chat;

/// <summary>
/// When something last happened in a group. The conversation list sorts people and groups against each
/// other, which it can only do if both answer that question - a group used to answer nothing, so they
/// sat in a block of their own sorted by name.
/// </summary>
public sealed class ChatGroupLastMessageTimeTests
{
    [Fact]
    public void A_new_group_counts_as_having_just_happened()
    {
        // Never null, so the list is totally ordered from the moment a group exists rather than needing
        // a second rule for the ones nobody has written in yet.
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        var group = ChatGroup.Create(Guid.NewGuid(), "Weekend trip");

        Assert.Equal(group.CreatedAtUtc, group.LastMessageAtUtc);
        Assert.True(group.LastMessageAtUtc > before);
    }

    [Fact]
    public void Posting_a_message_moves_it_forward()
    {
        var group = ChatGroup.Create(Guid.NewGuid(), "Weekend trip");
        var whenItWasMade = group.LastMessageAtUtc;

        group.MarkMessagePosted();

        Assert.True(group.LastMessageAtUtc >= whenItWasMade);
        Assert.NotEqual(group.CreatedAtUtc, group.LastMessageAtUtc);
    }

    [Fact]
    public void A_group_read_back_from_storage_keeps_the_time_it_was_stored_with()
    {
        // Rather than being taken for freshly made, which would put every group at the top on every load.
        var madeOn = DateTimeOffset.UtcNow.AddDays(-30);
        var lastSpokenOn = DateTimeOffset.UtcNow.AddDays(-2);

        var group = ChatGroup.FromPersistence(Guid.NewGuid(), "Weekend trip", Guid.NewGuid(), madeOn, lastSpokenOn, []);

        Assert.Equal(lastSpokenOn, group.LastMessageAtUtc);
        Assert.Equal(madeOn, group.CreatedAtUtc);
    }
}
