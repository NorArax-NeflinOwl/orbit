using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Which addresses the layout must not settle on arrival. Everything else it reaches is read by being
/// reached; a conversation is the exception, because its entries are about messages nobody has
/// necessarily looked at yet - see NewsSettler.SettledByThePageItself and ChatReadState.
/// </summary>
public sealed class NewsSettlerTests
{
    [Fact]
    public void A_conversation_settles_its_own_entries()
        => Assert.True(NewsSettler.SettledByThePageItself($"/chat/{Guid.NewGuid()}"));

    /// <summary>A group raises no entry per message, so what waits there is an invitation - and reaching it reads it.</summary>
    [Theory]
    [InlineData("/chat")]
    [InlineData("/chat/groups")]
    [InlineData("/chat/groups/2f1a4e0e-8f7e-4a51-9b3a-1d2c3b4a5f60")]
    [InlineData("/chat/groups/2f1a4e0e-8f7e-4a51-9b3a-1d2c3b4a5f60/info")]
    [InlineData("/tasks/2f1a4e0e-8f7e-4a51-9b3a-1d2c3b4a5f60")]
    [InlineData("/chat/not-an-id")]
    public void Every_other_page_is_read_by_being_reached(string path)
        => Assert.False(NewsSettler.SettledByThePageItself(path));

    /// <summary>A trailing slash is the same address, and the router treats it as one.</summary>
    [Fact]
    public void A_trailing_slash_makes_no_difference()
        => Assert.True(NewsSettler.SettledByThePageItself($"/chat/{Guid.NewGuid()}/"));
}
