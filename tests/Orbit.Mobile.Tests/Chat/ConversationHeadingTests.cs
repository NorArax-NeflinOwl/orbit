using Orbit.Core.Users;
using Orbit.Mobile.Data;
using Xunit;

namespace Orbit.Mobile.Tests.Chat;

/// <summary>
/// What the head of a conversation says about the person it is with. The screen used to carry their
/// name and nothing else - the same name the bar above it already showed - so neither of the two things
/// a reader wants to know before typing was anywhere on it: whether it will be seen now, and who else
/// could ever see it.
/// </summary>
public sealed class ConversationHeadingTests
{
    [Fact]
    public void A_conversation_says_where_they_are_and_that_it_is_sealed()
    {
        using var context = new ChatContext();
        var screen = context.Conversation();

        screen.Open(Somebody(context, nameof(PresenceStatus.Available), context.OtherPublicKeyBase64));

        Assert.Equal("Available · end-to-end encrypted", screen.Standing);
        Assert.True(screen.HasStanding);
    }

    /// <summary>
    /// Somebody who has never opened Orbit's chat has no key to encrypt for, so nothing here has been
    /// sealed and there is nothing to promise about - saying so would be a promise about an empty
    /// screen. Where they are is still worth saying.
    /// </summary>
    [Fact]
    public void Somebody_who_has_not_set_up_chat_is_promised_nothing()
    {
        using var context = new ChatContext();
        var screen = context.Conversation();

        screen.Open(Somebody(context, nameof(PresenceStatus.Away), publicKeyBase64: null));

        Assert.Equal("Away", screen.Standing);
    }

    /// <summary>
    /// A contact synced before presence existed carries no status at all, and not knowing where
    /// somebody is looks the same from here as their not being anywhere - see PresenceWords.
    /// </summary>
    [Fact]
    public void Somebody_whose_whereabouts_are_unknown_reads_as_offline()
    {
        using var context = new ChatContext();
        var screen = context.Conversation();

        screen.Open(Somebody(context, status: string.Empty, context.OtherPublicKeyBase64));

        Assert.Equal("Offline · end-to-end encrypted", screen.Standing);
    }

    /// <summary>The circle beside the name is coloured by whose it is, so the screen has to know.</summary>
    [Fact]
    public void The_conversation_knows_whose_circle_to_draw()
    {
        using var context = new ChatContext();
        var screen = context.Conversation();

        screen.Open(Somebody(context, nameof(PresenceStatus.Available), context.OtherPublicKeyBase64));

        Assert.Equal(context.OtherUserId, screen.ContactId);
        Assert.Equal("Bob", screen.Title);
    }

    private static LocalContact Somebody(ChatContext context, string status, string? publicKeyBase64)
    {
        var contact = LocalContact.ForSomebodyNotYetSpokenTo(
            context.OtherUserId, "bob", "Bob", publicKeyBase64);
        contact.PresenceStatus = status;
        return contact;
    }
}
