using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Where a screen goes when it is finished. The value comes off the address bar, so most of what
/// matters here is what is *refused*: Orbit's own "Save" must never be the thing that takes somebody to
/// another site.
/// </summary>
public sealed class ReturnToTests
{
    [Theory]
    [InlineData("/calendar")]
    [InlineData("/tasks/8b1f0f1e-0000-0000-0000-000000000000")]
    [InlineData("/inventory?highlight=1")]
    public void A_path_on_this_site_is_followed(string path)
        => Assert.Equal(path, ReturnTo.Safe(path));

    /// <summary>
    /// The ones that would leave Orbit. "//example.com" is another site however much it looks like a
    /// path, and a backslash is there because a browser may read it as the slash that makes one.
    /// </summary>
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("//example.com")]
    [InlineData("/\\example.com")]
    [InlineData("/tasks\\..\\..")]
    [InlineData("javascript:alert(1)")]
    [InlineData("tasks")]
    [InlineData("")]
    [InlineData(null)]
    public void Anything_that_would_leave_this_site_is_refused(string? refused)
        => Assert.Null(ReturnTo.Safe(refused));

    [Fact]
    public void A_control_character_is_refused()
        => Assert.Null(ReturnTo.Safe("/calendar\n/evil"));

    [Fact]
    public void A_link_carries_where_to_come_back_to()
        => Assert.Equal(
            "/tasks/1/edit?returnTo=%2Fcalendar",
            ReturnTo.Link("/tasks/1/edit", "/calendar"));

    /// <summary>Nothing to say means an ordinary link, not one with an empty parameter hanging off it.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com")]
    public void A_link_with_nowhere_worth_naming_is_left_alone(string? nowhere)
        => Assert.Equal("/tasks/1/edit", ReturnTo.Link("/tasks/1/edit", nowhere));

    [Fact]
    public void Where_to_go_now_is_what_was_asked_for_or_the_section_it_belongs_to()
    {
        Assert.Equal("/calendar", ReturnTo.Or("/calendar", "/tasks"));
        Assert.Equal("/tasks", ReturnTo.Or(null, "/tasks"));
        Assert.Equal("/tasks", ReturnTo.Or("https://example.com", "/tasks"));
    }
}
