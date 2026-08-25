using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Location;
using Orbit.Core.Location.GetSharedLocations;
using Orbit.Core.Location.ShareLocation;
using Orbit.Core.Location.StopSharingLocation;
using Xunit;

namespace Orbit.Api.Tests.Location;

/// <summary>
/// Covers what sharing a position promises: it reaches only the person it was shared with, the database
/// keeps one point rather than a trail, and stopping removes it rather than letting it go stale.
/// </summary>
public sealed class SharedLocationTests
{
    [Fact]
    public async Task Sharing_stores_one_point_the_recipient_can_fetch()
    {
        var context = new SharedLocationTestContext();

        await context.ShareAsync(context.SharerId, context.FriendId, "point-one", isContinuous: false);

        var received = Assert.Single(await context.SharedWithAsync(context.FriendId));
        Assert.Equal(context.SharerId, received.SharerUserId);
        Assert.Equal("point-one", received.CiphertextBase64);
        Assert.False(received.IsContinuous);
    }

    [Fact]
    public async Task Only_the_person_it_was_shared_with_can_fetch_it()
    {
        var context = new SharedLocationTestContext();

        await context.ShareAsync(context.SharerId, context.FriendId, "point-one", isContinuous: false);

        Assert.Empty(await context.SharedWithAsync(context.OtherUserId));
    }

    [Fact]
    public async Task Refreshing_replaces_the_point_instead_of_adding_another()
    {
        var context = new SharedLocationTestContext();
        await context.ShareAsync(context.SharerId, context.FriendId, "point-one", isContinuous: true);

        await context.ShareAsync(context.SharerId, context.FriendId, "point-two", isContinuous: true);
        await context.ShareAsync(context.SharerId, context.FriendId, "point-three", isContinuous: true);

        // This is the whole of "no history": a position refreshed every minute must leave one row, not
        // sixty an hour that together say where someone has been.
        var received = Assert.Single(await context.SharedWithAsync(context.FriendId));
        Assert.Equal("point-three", received.CiphertextBase64);
    }

    [Fact]
    public async Task A_one_off_share_can_be_turned_into_a_live_one_and_back()
    {
        var context = new SharedLocationTestContext();
        await context.ShareAsync(context.SharerId, context.FriendId, "point-one", isContinuous: false);

        await context.ShareAsync(context.SharerId, context.FriendId, "point-two", isContinuous: true);
        Assert.True(Assert.Single(await context.SharedWithAsync(context.FriendId)).IsContinuous);

        await context.ShareAsync(context.SharerId, context.FriendId, "point-three", isContinuous: false);
        Assert.False(Assert.Single(await context.SharedWithAsync(context.FriendId)).IsContinuous);
    }

    [Fact]
    public async Task Sharing_with_two_people_keeps_their_positions_apart()
    {
        var context = new SharedLocationTestContext();

        await context.ShareAsync(context.SharerId, context.FriendId, "for-friend", isContinuous: true);
        await context.ShareAsync(context.SharerId, context.OtherUserId, "for-other", isContinuous: true);

        // Each is sealed for one reader, so they are separate rows rather than one point sent twice.
        Assert.Equal("for-friend", Assert.Single(await context.SharedWithAsync(context.FriendId)).CiphertextBase64);
        Assert.Equal("for-other", Assert.Single(await context.SharedWithAsync(context.OtherUserId)).CiphertextBase64);
        Assert.Equal(2, (await context.SharedByAsync(context.SharerId)).Count);
    }

    [Fact]
    public async Task Stopping_removes_the_position_rather_than_leaving_it_stale()
    {
        var context = new SharedLocationTestContext();
        await context.ShareAsync(context.SharerId, context.FriendId, "point-one", isContinuous: true);

        await context.StopSharingAsync(context.SharerId, context.FriendId);

        Assert.Empty(await context.SharedWithAsync(context.FriendId));
        Assert.Empty(await context.SharedByAsync(context.SharerId));
    }

    [Fact]
    public async Task Stopping_with_everyone_clears_every_share_at_once()
    {
        var context = new SharedLocationTestContext();
        await context.ShareAsync(context.SharerId, context.FriendId, "for-friend", isContinuous: true);
        await context.ShareAsync(context.SharerId, context.OtherUserId, "for-other", isContinuous: true);

        await context.StopSharingAsync(context.SharerId, recipientUserId: null);

        Assert.Empty(await context.SharedByAsync(context.SharerId));
    }

    [Fact]
    public async Task Stopping_something_that_was_never_started_is_not_an_error()
    {
        var context = new SharedLocationTestContext();

        // The end state asked for is "they can't see me", which is already true.
        Assert.True(await context.StopSharingAsync(context.SharerId, context.FriendId));
    }

    [Fact]
    public async Task Stopping_one_person_leaves_the_others_alone()
    {
        var context = new SharedLocationTestContext();
        await context.ShareAsync(context.SharerId, context.FriendId, "for-friend", isContinuous: true);
        await context.ShareAsync(context.SharerId, context.OtherUserId, "for-other", isContinuous: true);

        await context.StopSharingAsync(context.SharerId, context.FriendId);

        Assert.Empty(await context.SharedWithAsync(context.FriendId));
        Assert.Single(await context.SharedWithAsync(context.OtherUserId));
    }

    [Fact]
    public async Task Sharing_with_someone_you_have_no_chat_with_is_refused()
    {
        var context = new SharedLocationTestContext();

        // A position is not something to be able to push at a stranger who never agreed to hear from
        // you - the same rule adding someone to a group follows.
        await Assert.ThrowsAsync<InvalidRequestException>(
            () => context.ShareAsync(context.SharerId, Guid.NewGuid(), "point-one", isContinuous: false));
    }

    [Fact]
    public void Sharing_with_yourself_is_refused()
    {
        var userId = Guid.NewGuid();

        Assert.Throws<InvalidRequestException>(
            () => SharedLocation.Create(userId, userId, "ciphertext", "nonce", isContinuous: false));
    }

    private sealed class SharedLocationTestContext
    {
        private readonly InMemorySharedLocationRepository _sharedLocationRepository = new();
        private readonly InMemoryContactRepository _contactRepository = new();

        public Guid SharerId { get; } = Guid.NewGuid();
        public Guid FriendId { get; } = Guid.NewGuid();
        public Guid OtherUserId { get; } = Guid.NewGuid();

        public SharedLocationTestContext()
        {
            // Both are people the sharer already chats with, which is what sharing requires.
            _contactRepository.EnsureContactAsync(SharerId, FriendId, DateTimeOffset.UtcNow, CancellationToken.None)
                .GetAwaiter().GetResult();
            _contactRepository.EnsureContactAsync(SharerId, OtherUserId, DateTimeOffset.UtcNow, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        public Task<bool> ShareAsync(Guid sharerId, Guid recipientId, string ciphertext, bool isContinuous)
            => new ShareLocationCommandHandler(_sharedLocationRepository, _contactRepository)
                .HandleAsync(new ShareLocationCommand(sharerId, recipientId, ciphertext, "nonce", isContinuous), CancellationToken.None);

        public Task<bool> StopSharingAsync(Guid sharerId, Guid? recipientUserId)
            => new StopSharingLocationCommandHandler(_sharedLocationRepository)
                .HandleAsync(new StopSharingLocationCommand(sharerId, recipientUserId), CancellationToken.None);

        public Task<IReadOnlyList<SharedLocation>> SharedWithAsync(Guid recipientId)
            => new GetSharedLocationsQueryHandler(_sharedLocationRepository)
                .HandleAsync(new GetSharedLocationsQuery(recipientId), CancellationToken.None);

        public Task<IReadOnlyList<SharedLocation>> SharedByAsync(Guid sharerId)
            => new GetOwnLocationSharesQueryHandler(_sharedLocationRepository)
                .HandleAsync(new GetOwnLocationSharesQuery(sharerId), CancellationToken.None);
    }
}
