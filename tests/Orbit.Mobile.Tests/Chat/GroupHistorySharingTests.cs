using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// Giving somebody who joined a group late the conversation they arrived after.
///
/// The server holds no key to any of it, so this can only happen on the device of a member who can
/// already read the group: they open each message under the key it was sealed with and seal it again
/// under the one they share with the newcomer. What matters here is that what comes out the other end
/// is genuinely readable by that newcomer, which is why these tests do the recipient's decryption
/// themselves rather than trusting a count.
/// </summary>
public sealed class GroupHistorySharingTests : IDisposable
{
    private readonly FakeChatServer _server = new(new FakeTimeProvider(DateTimeOffset.Parse("2026-08-30T10:00:00Z")));
    private readonly FakeUsersServer _users = new();
    private readonly InMemoryChatKeyStorage _keyStorage = new();

    private readonly ChatIdentity _mine = ChatIdentity.Create();
    private readonly ChatIdentity _authors = ChatIdentity.Create();
    private readonly ChatIdentity _newcomers = ChatIdentity.Create();

    private readonly Guid _authorUserId = Guid.NewGuid();
    private readonly Guid _newcomerUserId = Guid.NewGuid();

    [Fact]
    public async Task The_past_is_opened_and_sealed_again_so_the_newcomer_can_read_it()
    {
        var group = AGroupWithOneMessage("See you at six", out var groupMessageId);

        var shared = await ASharing().ShareWithAsync(group.Id, _newcomerUserId);

        Assert.Equal(1, shared);
        var handedOver = Assert.Single(_server.HistoryHandedOver);
        Assert.Equal(_newcomerUserId, handedOver.RecipientUserId);

        var copy = Assert.Single(handedOver.Copies);
        Assert.Equal(groupMessageId, copy.GroupMessageId);
        // The point of the whole exercise: their key opens it.
        Assert.Equal(
            "See you at six",
            _newcomers.Decrypt(_mine.PublicKeyBase64, new EncryptedText(copy.CiphertextBase64, copy.NonceBase64)));
    }

    /// <summary>
    /// A message sealed under a key pair this device has since replaced cannot be opened here either,
    /// and one nobody can read is not something to pass on as ciphertext the newcomer would stare at.
    /// </summary>
    [Fact]
    public async Task A_message_this_device_cannot_open_is_left_behind()
    {
        var group = AGroupWithOneMessage("See you at six", out _);
        using var strangers = ChatIdentity.Create();
        var unreadable = strangers.Encrypt(strangers.PublicKeyBase64, "Sealed for nobody here");
        _server.AddIncomingGroupCopy(
            group.Id, Guid.NewGuid(), _authorUserId, OwnUserId, unreadable.CiphertextBase64, unreadable.NonceBase64);

        var shared = await ASharing().ShareWithAsync(group.Id, _newcomerUserId);

        // The readable one still goes; the other simply does not.
        Assert.Equal(1, shared);
        Assert.Single(Assert.Single(_server.HistoryHandedOver).Copies);
    }

    [Fact]
    public async Task A_group_with_nothing_readable_in_it_hands_nothing_over()
    {
        var group = _server.AddGroup("Trip", _authorUserId, _newcomerUserId);
        Knows(_authorUserId, _authors);
        Knows(_newcomerUserId, _newcomers);

        var shared = await ASharing().ShareWithAsync(group.Id, _newcomerUserId);

        // Nothing offered rather than an empty hand-off posted, which the server would have to answer.
        Assert.Equal(0, shared);
        Assert.Empty(_server.HistoryHandedOver);
    }

    /// <summary>
    /// Somebody who has never signed in has no key to seal anything for. Said rather than counted as
    /// nothing shared, because it is a wait rather than a dead end - the group screen tells them so.
    /// </summary>
    [Fact]
    public async Task A_recipient_with_no_key_on_file_is_refused_rather_than_answered_with_nothing()
    {
        // Never signed in, so nothing anywhere knows a key for them.
        var group = AGroupWithOneMessage("See you at six", out _, newcomerHasSignedIn: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ASharing().ShareWithAsync(group.Id, _newcomerUserId));

        Assert.Empty(_server.HistoryHandedOver);
    }

    private Guid OwnUserId => _server.CallerUserId;

    /// <summary>A group holding one message the author sealed for this account, as a real one would.</summary>
    private Orbit.Contracts.Chat.ChatGroupDto AGroupWithOneMessage(
        string text, out Guid groupMessageId, bool newcomerHasSignedIn = true)
    {
        var group = _server.AddGroup("Trip", _authorUserId, _newcomerUserId);
        Knows(_authorUserId, _authors);
        if (newcomerHasSignedIn)
        {
            Knows(_newcomerUserId, _newcomers);
        }

        groupMessageId = Guid.NewGuid();
        var sealedForMe = _authors.Encrypt(_mine.PublicKeyBase64, text);
        _server.AddIncomingGroupCopy(
            group.Id, groupMessageId, _authorUserId, OwnUserId, sealedForMe.CiphertextBase64, sealedForMe.NonceBase64);

        return group;
    }

    /// <summary>Somebody this account can reach, with the public key they really hold.</summary>
    private void Knows(Guid userId, ChatIdentity identity)
    {
        _server.AddContact(userId, identity.PublicKeyBase64);
        _users.Add(userId, "Somebody", identity.PublicKeyBase64);
    }

    private GroupHistorySharing ASharing()
    {
        _keyStorage.WritePrivateKeyJwkAsync(OwnUserId, _mine.ExportPrivateKeyJwk()).GetAwaiter().GetResult();
        var session = new SessionStore(new InMemorySessionStorage(
            new UserSession("access", "refresh", OwnUserId, "me@orbit.example", "Me")));

        var chatClient = new ChatClient(_server.ToHttpClient());
        return GroupHistory.SharedBy(chatClient, session, _users, _keyStorage);
    }

    public void Dispose()
    {
        _server.Dispose();
        _users.Dispose();
        _mine.Dispose();
        _authors.Dispose();
        _newcomers.Dispose();
    }
}
