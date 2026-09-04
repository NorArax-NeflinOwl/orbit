using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Web.Components;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// What this browser is allowed to keep. Orbit sets no cookies at all, so the dialog behind the
/// "Manage cookies" link is about local storage - and what these tests hold is that it says so, that
/// the necessary category cannot be declined, and that a choice actually reaches the storage rather
/// than only the screen.
/// </summary>
public sealed class ManageCookiesDialogTests : OrbitTestContext
{
    [Fact]
    public void It_opens_on_what_this_browser_has_already_been_allowed()
    {
        SetUpConsent(preferences: false, diagnostics: true);

        var cut = RenderComponent<ManageCookiesDialog>();

        var boxes = cut.FindAll(".cookies-category-head input").ToList();
        Assert.Equal(3, boxes.Count);
        Assert.True(boxes[0].HasAttribute("disabled"));
        Assert.False(boxes[1].HasAttribute("checked"));
        Assert.True(boxes[2].HasAttribute("checked"));
    }

    [Fact]
    public void The_necessary_category_can_never_be_turned_off()
    {
        SetUpConsent(preferences: true, diagnostics: true);

        var cut = RenderComponent<ManageCookiesDialog>();

        // Shown rather than hidden: a reader deciding what to allow should see everything that is being
        // kept, including the part that is not up for discussion.
        var necessary = cut.FindAll(".cookies-category-head input").First();
        Assert.True(necessary.HasAttribute("checked"));
        Assert.True(necessary.HasAttribute("disabled"));
    }

    [Fact]
    public void Rejecting_the_optional_categories_reaches_the_storage_and_says_what_happened()
    {
        SetUpConsent(preferences: true, diagnostics: true);
        JSInterop.SetupVoid("OrbitStorageConsent.set", false, false).SetVoidResult();
        var cut = RenderComponent<ManageCookiesDialog>();

        cut.FindAll(".dialog-footer button").First().Click();

        Assert.Single(JSInterop.Invocations["OrbitStorageConsent.set"]);
        Assert.Contains("has been cleared", cut.Find(".info").TextContent);
    }

    [Fact]
    public void Accepting_everything_reaches_the_storage_the_same_way()
    {
        SetUpConsent(preferences: false, diagnostics: false);
        JSInterop.SetupVoid("OrbitStorageConsent.set", true, true).SetVoidResult();
        var cut = RenderComponent<ManageCookiesDialog>();

        cut.FindAll(".dialog-footer button").ElementAt(1).Click();

        Assert.Single(JSInterop.Invocations["OrbitStorageConsent.set"]);
    }

    [Fact]
    public void It_says_Orbit_sets_no_cookies_rather_than_letting_the_link_stand_as_the_whole_answer()
    {
        SetUpConsent(preferences: true, diagnostics: true);

        var cut = RenderComponent<ManageCookiesDialog>();

        Assert.Contains("Orbit sets no cookies", cut.Find(".dialog-body").TextContent);
    }

    /// <summary>
    /// The wrapper the dialog reads through - see storageConsent.js, which is a plain script rather than
    /// a module, so these are named calls rather than a set-up module.
    /// </summary>
    private void SetUpConsent(bool preferences, bool diagnostics)
    {
        JSInterop.Setup<StorageConsent>("OrbitStorageConsent.get")
            .SetResult(new StorageConsent(preferences, diagnostics));
        JSInterop.Setup<StoredKeyCounts>("OrbitStorageConsent.counts")
            .SetResult(new StoredKeyCounts(Necessary: 2, Preferences: 5, Diagnostics: 1));
        Services.AddScoped<BrowserStorageConsent>();
    }
}
