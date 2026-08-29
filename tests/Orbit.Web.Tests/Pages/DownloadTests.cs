using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// The page somebody reaches before they have the app, and often before they have signed in anywhere.
/// What matters is that it never offers a link it does not have: a button leading to an empty address
/// looks like a broken download rather than a build that was never published.
/// </summary>
public sealed class DownloadTests : OrbitTestContext
{
    private const string AndroidRelease = "https://example.invalid/orbit-0.1.0.apk";
    private const string TestFlightInvitation = "https://testflight.apple.com/join/example";

    [Fact]
    public void A_published_Android_build_is_offered_as_a_link_to_it()
    {
        var page = Render(new MobileAppDownloads(AndroidRelease, string.Empty));

        Assert.Equal(AndroidRelease, page.Find("a.btn-primary").GetAttribute("href"));
    }

    [Fact]
    public void Nothing_is_offered_where_no_build_has_been_published()
    {
        var page = Render(new MobileAppDownloads(string.Empty, string.Empty));

        Assert.Empty(page.FindAll("a.btn-primary"));
        Assert.Contains("No Android build has been published yet.", page.Markup);
        Assert.Contains("No iPhone build has been published yet.", page.Markup);
    }

    /// <summary>
    /// The two are published separately - one needs a Mac and an Apple account, the other does not - so
    /// the page has to be right with only one of them in hand.
    /// </summary>
    [Fact]
    public void One_platform_being_published_says_nothing_about_the_other()
    {
        var page = Render(new MobileAppDownloads(string.Empty, TestFlightInvitation));

        Assert.Contains("No Android build has been published yet.", page.Markup);
        Assert.Equal(TestFlightInvitation, page.Find("a.btn-primary").GetAttribute("href"));
    }

    private IRenderedComponent<Download> Render(MobileAppDownloads downloads)
    {
        Services.AddSingleton(downloads);
        return RenderComponent<Download>();
    }
}
