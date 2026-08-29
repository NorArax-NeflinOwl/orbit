using Orbit.Contracts.Chat;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Screens.Chat;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// Answering one particular message. Like a forward, the server learns nothing about it - it holds
/// ciphertext either way - so what is being answered travels inside the plaintext, and the quote is
/// carried rather than looked up: the original may have been edited or deleted since.
/// </summary>
public sealed class ReplyingTests
{
    [Fact]
    public async Task A_reply_arrives_showing_its_words_and_what_it_answers()
    {
        using var context = new ChatContext();
        var body = ReplyMessage.Wrap(Guid.NewGuid(), "are we still on for Tuesday?", "yes, at seven");
        var sealedText = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, body);
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, sealedText.CiphertextBase64, sealedText.NonceBase64);
        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        var received = Assert.Single(await context.ReadConversationAsync());

        // The words, not the JSON they travelled in.
        Assert.Equal("yes, at seven", received.Text);
        Assert.Equal("are we still on for Tuesday?", received.QuotedPreview);
        Assert.True(received.IsReply);
    }

    /// <summary>A long quote is cut down: enough to recognise which message is meant, not a second copy of it.</summary>
    [Fact]
    public void A_long_quote_is_carried_shortened()
    {
        var answered = new string('a', ReplyMessagePayload.MaximumPreviewLength + 50);

        var payload = ReplyMessage.TryUnwrap(ReplyMessage.Wrap(Guid.NewGuid(), answered, "short answer"));

        Assert.NotNull(payload);
        Assert.True(payload.ReplyToPreview.Length <= ReplyMessagePayload.MaximumPreviewLength + 1);
        Assert.EndsWith("…", payload.ReplyToPreview);
    }

    /// <summary>
    /// Text that happens to be JSON but carries no marker is ordinary text - a message reading "{}" is a
    /// message reading "{}", not a broken payload.
    /// </summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("not json at all")]
    [InlineData("")]
    public void Ordinary_text_is_not_mistaken_for_a_reply(string text)
        => Assert.Null(ReplyMessage.TryUnwrap(text));

    [Fact]
    public async Task Answering_a_message_sends_what_it_answers_along_with_it()
    {
        using var context = new ChatContext();
        var screen = await OpenWithOneMessageFromThemAsync(context);

        screen.StartReplyingCommand.Execute(screen.Messages.Single(message => !message.IsMine));
        screen.Draft = "yes";
        await screen.SendCommand.ExecuteAsync(null);

        var sent = context.Server.Messages.Single(message => message.SenderUserId == context.OwnUserId);
        var payload = ReplyMessage.TryUnwrap(context.OpenAsTheOtherParty(sent)!);
        Assert.NotNull(payload);
        Assert.Equal("yes", payload.Content);
        Assert.Equal("are we still on?", payload.ReplyToPreview);
    }

    /// <summary>
    /// The strip above the box says what is being answered, and stops saying it once the answer has
    /// gone: the next message is addressed to the conversation again unless the reader says otherwise.
    /// </summary>
    [Fact]
    public async Task The_strip_says_what_is_being_answered_until_the_answer_is_sent()
    {
        using var context = new ChatContext();
        var screen = await OpenWithOneMessageFromThemAsync(context);

        screen.StartReplyingCommand.Execute(screen.Messages.Single(message => !message.IsMine));
        Assert.True(screen.HasReplyingTo);
        Assert.Equal("are we still on?", screen.ReplyingToPreview);

        screen.Draft = "yes";
        await screen.SendCommand.ExecuteAsync(null);

        Assert.False(screen.HasReplyingTo);
    }

    /// <summary>
    /// Deciding what to answer must not cost the reader what they had already typed. Rewriting a
    /// message does clear the box, and answering one borrows the same box - which is exactly why this
    /// is worth pinning down.
    /// </summary>
    [Fact]
    public async Task Starting_to_answer_keeps_a_draft_already_typed()
    {
        using var context = new ChatContext();
        var screen = await OpenWithOneMessageFromThemAsync(context);

        screen.Draft = "half a thought";
        screen.StartReplyingCommand.Execute(screen.Messages.Single(message => !message.IsMine));

        Assert.Equal("half a thought", screen.Draft);
    }

    [Fact]
    public async Task Putting_the_answer_down_sends_the_next_message_to_the_conversation()
    {
        using var context = new ChatContext();
        var screen = await OpenWithOneMessageFromThemAsync(context);

        screen.StartReplyingCommand.Execute(screen.Messages.Single(message => !message.IsMine));
        screen.CancelReplyingCommand.Execute(null);
        screen.Draft = "unrelated";
        await screen.SendCommand.ExecuteAsync(null);

        var sent = context.Server.Messages.Single(message => message.SenderUserId == context.OwnUserId);
        Assert.Equal("unrelated", context.OpenAsTheOtherParty(sent));
    }

    /// <summary>
    /// A message still in the queue has never been encrypted, so the screen reads its plaintext
    /// straight - and before this it read the payload straight too, showing the sender raw JSON until
    /// the message left the phone. True of a forward waiting to go out as well.
    /// </summary>
    [Fact]
    public async Task An_answer_waiting_to_go_out_shows_its_words_rather_than_the_payload()
    {
        using var context = new ChatContext();
        context.Server.IsUnreachable = true;
        context.GiveTheOtherPartyAPublishedKey();

        await context.Sender.SendAsync(
            context.OtherUserId, ReplyMessage.Wrap(Guid.NewGuid(), "are we still on?", "yes"));

        var queued = Assert.Single(await context.ReadConversationAsync());
        Assert.True(queued.IsWaitingToSend);
        Assert.Equal("yes", queued.Text);
        Assert.Equal("are we still on?", queued.QuotedPreview);
    }

    /// <summary>One message from the other party, already synced, with the screen open on it.</summary>
    private static async Task<ConversationViewModel> OpenWithOneMessageFromThemAsync(
        ChatContext context)
    {
        context.GiveTheOtherPartyAPublishedKey();
        var fromThem = context.OtherIdentity.Encrypt(context.OwnPublicKeyBase64, "are we still on?");
        context.Server.AddIncoming(
            context.OtherUserId, context.OwnUserId, fromThem.CiphertextBase64, fromThem.NonceBase64);
        await context.Synchronizer.SynchroniseConversationAsync(context.OtherUserId);

        var screen = context.Conversation();
        await screen.LoadCommand.ExecuteAsync(null);
        return screen;
    }
}
