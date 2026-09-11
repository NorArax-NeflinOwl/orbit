using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// The trail against a navigation manager: that it hears every arrival, that an address a page asked for
/// and the one the browser reports compare equal however they were spelled, and that a finish is either
/// a step back through the browser or a replacement - never a new entry on top. InAppHistoryTests holds
/// the rules themselves.
/// </summary>
public sealed class NavigationTrailTests : TestContext
{
    private readonly FakeNavigationManager _navigationManager;

    public NavigationTrailTests()
    {
        _navigationManager = Services.GetRequiredService<FakeNavigationManager>();
        JSInterop.SetupVoid("history.go", _ => true).SetVoidResult();
    }

    private NavigationTrail Trail() => new(_navigationManager, JSInterop.JSRuntime);

    [Fact]
    public async Task Finishing_onto_the_page_just_behind_steps_back_through_the_browser()
    {
        var trail = Trail();
        _navigationManager.NavigateTo("/notes");
        _navigationManager.NavigateTo("/notes/new");

        await trail.FinishAsync("/notes");

        Assert.Equal(-1, JSInterop.VerifyInvoke("history.go").Arguments[0]);
    }

    /// <summary>
    /// The address the summary is on carries its own returnTo, escaped once; the form reads it back
    /// unescaped and hands it over. The two have to be recognised as the same page.
    /// </summary>
    [Fact]
    public async Task A_summarys_own_address_is_recognised_whichever_way_it_was_spelled()
    {
        var trail = Trail();
        _navigationManager.NavigateTo("/notes/1?returnTo=%2F");
        _navigationManager.NavigateTo(ReturnTo.Link("/notes/1/edit", trail.Here));

        await trail.FinishAsync("/notes/1?returnTo=%2F");

        Assert.Equal(-1, JSInterop.VerifyInvoke("history.go").Arguments[0]);
    }

    [Fact]
    public async Task A_page_with_nothing_of_orbits_behind_it_is_replaced()
    {
        var trail = Trail();
        _navigationManager.NavigateTo("/calendar");
        _navigationManager.NavigateTo("/notes/1/edit");

        await trail.FinishAsync("/notes");

        Assert.Equal("http://localhost/notes", _navigationManager.Uri);
        Assert.True(_navigationManager.History.First().Options.ReplaceHistoryEntry);
        Assert.Empty(JSInterop.Invocations["history.go"]);
    }

    /// <summary>The browser's own Back is heard too, so the next finish knows what is behind it now.</summary>
    [Fact]
    public async Task The_browsers_own_Back_keeps_the_trail_true()
    {
        var trail = Trail();
        _navigationManager.NavigateTo("/notes");
        _navigationManager.NavigateTo("/notes/1");
        _navigationManager.NavigateTo("/notes");
        _navigationManager.NavigateTo("/notes/new");

        await trail.FinishAsync("/notes");

        Assert.Equal(-1, JSInterop.VerifyInvoke("history.go").Arguments[0]);
    }

    /// <summary>
    /// Deleting from a form opened on the thing's own page: back onto the page, and once the browser is
    /// there it is replaced with the list - so neither page is left to open on "no longer exists".
    /// </summary>
    [Fact]
    public async Task Deleting_steps_back_onto_the_things_page_and_replaces_it()
    {
        var trail = Trail();
        _navigationManager.NavigateTo("/notes/1?returnTo=%2F");
        _navigationManager.NavigateTo("/notes/1/edit?returnTo=%2Fnotes%2F1%3FreturnTo%3D%252F");

        await trail.FinishAfterDeletingAsync("/notes/1", destination: "/notes/1?returnTo=%2F", section: "/notes");
        // What the browser does with the step back: arrive on the summary.
        _navigationManager.NavigateTo("/notes/1?returnTo=%2F");

        Assert.Equal(-1, JSInterop.VerifyInvoke("history.go").Arguments[0]);
        Assert.Equal("http://localhost/notes", _navigationManager.Uri);
        Assert.True(_navigationManager.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public void Here_is_the_page_on_screen_with_its_query()
    {
        var trail = Trail();
        _navigationManager.NavigateTo("/notes/1?returnTo=%2F");

        Assert.Equal("/notes/1?returnTo=%2F", trail.Here);
    }
}
