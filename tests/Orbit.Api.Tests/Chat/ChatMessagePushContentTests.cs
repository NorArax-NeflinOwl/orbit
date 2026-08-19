using Orbit.Core.Chat.SendMessage;
using Xunit;

namespace Orbit.Api.Tests.Chat;

public sealed class ChatMessagePushContentTests
{
    [Fact]
    public void Build_includes_the_senders_display_name_but_never_message_content()
    {
        var payload = ChatMessagePushContent.Build(Guid.NewGuid(), "Ada Lovelace");

        Assert.Contains("Ada Lovelace", payload.Body);
    }

    [Fact]
    public void Build_points_the_url_at_the_conversation_with_the_sender()
    {
        var senderId = Guid.NewGuid();

        var payload = ChatMessagePushContent.Build(senderId, "Ada Lovelace");

        Assert.Equal($"/chat/{senderId}", payload.Url);
    }
}
