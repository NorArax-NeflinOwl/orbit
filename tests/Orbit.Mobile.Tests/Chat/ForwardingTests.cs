using Orbit.Contracts.Chat;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// Passing a message on. The server learns nothing about it - it holds ciphertext either way - so the
/// fact travels inside the plaintext, and a forward is a re-encryption rather than a re-send: the
/// original ciphertext is sealed between two people and means nothing to a third.
/// </summary>
public sealed class ForwardingTests
{
    private static readonly LocalContact Somebody = LocalContact.ForSomebodyNotYetSpokenTo(
        Guid.NewGuid(), "carol", "Carol", "a-key");

    [Fact]
    public async Task Forwarding_somebody_elses_message_carries_who_wrote_it()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        var fromThem = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, "the original words");
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, fromThem.CiphertextBase64, fromThem.NonceBase64);
        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        var received = Assert.Single(await context.ReadConversationAsync());
        var target = context.Server.AddContact(Guid.NewGuid(), context.ThirdPublicKeyBase64);
        await context.Forwarder.ForwardAsync(
            received, context.OtherUserId, "Bob",
            LocalContact.ForSomebodyNotYetSpokenTo(target.UserId, target.UserName, target.DisplayName, target.PublicKeyBase64));

        // What reaches them is a fresh sealing for them, carrying the attribution inside.
        var forwarded = context.Server.Messages.Single(message => message.RecipientUserId == target.UserId);
        var payload = ForwardedMessage.TryUnwrap(context.OpenAsTheThirdParty(forwarded)!);
        Assert.NotNull(payload);
        Assert.Equal("the original words", payload.Content);
        Assert.Equal("Bob", payload.OriginalAuthorDisplayName);
    }

    [Fact]
    public async Task Forwarding_your_own_message_sends_it_as_ordinary_text()
    {
        // Indistinguishable from typing it again, so wrapping it would cost the recipient an unwrap and
        // claim an attribution that says nothing.
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        await context.Sender.SendAsync(context.OtherUserId, "my own words");
        var mine = Assert.Single(await context.ReadConversationAsync());

        var target = context.Server.AddContact(Guid.NewGuid(), context.ThirdPublicKeyBase64);
        await context.Forwarder.ForwardAsync(
            mine, context.OwnUserId, "Me",
            LocalContact.ForSomebodyNotYetSpokenTo(target.UserId, target.UserName, target.DisplayName, target.PublicKeyBase64));

        var forwarded = context.Server.Messages.Single(message => message.RecipientUserId == target.UserId);
        var text = context.OpenAsTheThirdParty(forwarded);
        Assert.Equal("my own words", text);
        Assert.Null(ForwardedMessage.TryUnwrap(text!));
    }

    [Fact]
    public async Task A_forward_arrives_showing_its_words_and_who_wrote_them()
    {
        using var context = new ChatContext();
        var body = ForwardedMessage.Wrap(
            isMine: false, originalAuthorUserId: Guid.NewGuid(), "Carol", "something Carol said");
        var sealedText = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, body);
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, sealedText.CiphertextBase64, sealedText.NonceBase64);
        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        var received = Assert.Single(await context.ReadConversationAsync());
        // The words, not the JSON they travelled in.
        Assert.Equal("something Carol said", received.Text);
        Assert.Equal("Carol", received.ForwardedFromDisplayName);
        Assert.True(received.WasForwarded);
    }

    [Fact]
    public async Task Passing_a_forward_along_still_credits_whoever_wrote_it()
    {
        // Attribution stays with the author rather than walking the chain - otherwise the third person
        // to touch a message would be credited with it.
        using var context = new ChatContext();
        var body = ForwardedMessage.Wrap(isMine: false, Guid.NewGuid(), "Carol", "something Carol said");
        var sealedText = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, body);
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, sealedText.CiphertextBase64, sealedText.NonceBase64);
        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        var received = Assert.Single(await context.ReadConversationAsync());
        var target = context.Server.AddContact(Guid.NewGuid(), context.ThirdPublicKeyBase64);
        await context.Forwarder.ForwardAsync(
            received, context.OtherUserId, "Bob",
            LocalContact.ForSomebodyNotYetSpokenTo(target.UserId, target.UserName, target.DisplayName, target.PublicKeyBase64));

        var forwarded = context.Server.Messages.Single(message => message.RecipientUserId == target.UserId);
        var payload = ForwardedMessage.TryUnwrap(context.OpenAsTheThirdParty(forwarded)!);
        Assert.Equal("Carol", payload!.OriginalAuthorDisplayName);
    }

    [Fact]
    public void Text_that_merely_looks_like_json_is_left_alone()
    {
        // A message reading "{}" is a message reading "{}", not a broken payload.
        Assert.Null(ForwardedMessage.TryUnwrap("{}"));
        Assert.Null(ForwardedMessage.TryUnwrap("{\"type\":\"something-else\",\"content\":\"hi\"}"));
        Assert.Null(ForwardedMessage.TryUnwrap("not json at all"));
        Assert.Null(ForwardedMessage.TryUnwrap("{ this never parses"));
    }

    [Fact]
    public async Task A_message_that_could_not_be_opened_here_offers_no_forwarding()
    {
        // There is nothing to re-encrypt for somebody else.
        using var context = new ChatContext();
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, "AAAAAAAAAAAAAAAAAAAAAA==", "AAAAAAAAAAAAAAAA");
        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        var unopenable = Assert.Single(await context.ReadConversationAsync());
        Assert.True(unopenable.CannotBeOpened);
        Assert.False(unopenable.CanBeForwarded);
    }

    [Fact]
    public async Task A_message_still_waiting_to_go_out_offers_no_forwarding()
    {
        using var context = new ChatContext();
        context.GiveTheOtherPartyAPublishedKey();
        context.Server.IsUnreachable = true;
        await context.Sender.SendAsync(context.OtherUserId, "typed with no signal");

        var queued = Assert.Single(await context.ReadConversationAsync());
        Assert.True(queued.IsWaitingToSend);
        Assert.False(queued.CanBeForwarded);
    }
}
