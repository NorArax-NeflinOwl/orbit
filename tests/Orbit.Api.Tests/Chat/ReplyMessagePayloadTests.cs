using Orbit.Contracts.Chat;
using Xunit;

namespace Orbit.Api.Tests.Chat;

/// <summary>
/// Covers the preview a reply carries. It is carried rather than looked up, so it has to be short enough
/// not to repeat the original wholesale and long enough to recognise which message is meant.
/// </summary>
public sealed class ReplyMessagePayloadTests
{
    [Fact]
    public void A_short_message_is_quoted_whole()
        => Assert.Equal("See you at six", ReplyMessagePayload.Preview("See you at six"));

    [Fact]
    public void A_long_message_is_cut_and_marked()
    {
        var preview = ReplyMessagePayload.Preview(new string('a', 300));

        Assert.EndsWith("…", preview);
        Assert.True(preview.Length <= ReplyMessagePayload.MaximumPreviewLength + 1);
    }

    [Fact]
    public void A_message_of_exactly_the_limit_is_not_cut()
    {
        var exact = new string('a', ReplyMessagePayload.MaximumPreviewLength);

        Assert.Equal(exact, ReplyMessagePayload.Preview(exact));
    }

    [Fact]
    public void A_cut_preview_does_not_end_on_a_dangling_space()
    {
        // "…had a look at   …" reads as a typo rather than a cut.
        var preview = ReplyMessagePayload.Preview(new string('a', ReplyMessagePayload.MaximumPreviewLength - 1) + "   tail");

        Assert.DoesNotContain(" …", preview);
    }

    [Fact]
    public void An_empty_message_previews_as_nothing()
        => Assert.Equal(string.Empty, ReplyMessagePayload.Preview(string.Empty));

    [Fact]
    public void A_payload_declares_its_own_type()
    {
        // Chat.razor decides what a decrypted message is by this field - see TryParseShare's siblings.
        var payload = new ReplyMessagePayload(Guid.NewGuid(), "See you at six", "I'll be there");

        Assert.Equal(ReplyMessagePayload.MessageType, payload.Type);
    }
}
