using Bunit;
using Microsoft.AspNetCore.Components;
using Orbit.Core.Users;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The small controls several screens share. They are one component each precisely so the gesture means
/// the same thing wherever somebody meets it, which is what these hold in place.
/// </summary>
public sealed class SharedControlTests : OrbitTestContext
{
    /// <summary>
    /// A wait shows Orbit's icon inside a turning ring, and the word only for a screen reader: "Loading…"
    /// alone in a corner reads as a page that has gone wrong rather than one that is coming, which is
    /// the reading the boot screen was written to avoid.
    /// </summary>
    [Fact]
    public void A_wait_draws_the_icon_inside_the_ring_and_keeps_the_word_for_a_screen_reader()
    {
        var cut = RenderComponent<Loading>();

        var ring = cut.Find(".loading-orbit-ring");
        Assert.Equal("true", ring.GetAttribute("aria-hidden"));
        Assert.NotNull(ring.QuerySelector(".loading-orbit-spinner"));
        Assert.NotNull(ring.QuerySelector("svg.loading-orbit-icon"));
        Assert.Equal("status", cut.Find(".loading-orbit").GetAttribute("role"));
        Assert.Equal("Loading…", cut.Find(".visually-hidden").TextContent);
        // No wordmark and no line: this is a wait inside a page that already says Orbit at the top.
        Assert.Empty(cut.FindAll(".loading-orbit-name"));
        Assert.Empty(cut.FindAll(".loading-orbit-says"));
    }

    /// <summary>
    /// A wait that has the whole window says whose window it is and what it is waiting for - the boot
    /// screen's own lockup, so a wait that starts before Blazor and one that starts after it look the
    /// same. The line replaces the hidden word rather than joining it: two of them is the same sentence
    /// read out twice.
    /// </summary>
    [Fact]
    public void A_wait_with_the_whole_window_names_itself_and_what_it_is_waiting_for()
    {
        var cut = RenderComponent<Loading>(parameters => parameters
            .Add(loading => loading.ShowsName, true)
            .Add(loading => loading.Says, "Checking permissions…"));

        Assert.Equal("Orbit", cut.Find(".loading-orbit-name").TextContent);
        Assert.Equal("Checking permissions…", cut.Find(".loading-orbit-says").TextContent);
        Assert.Empty(cut.FindAll(".visually-hidden"));
    }

    /// <summary>The small version in a corner of a screen is the same ring and icon, a size down.</summary>
    [Fact]
    public void An_inline_wait_is_the_same_ring_a_size_down()
    {
        var cut = RenderComponent<Loading>(parameters => parameters.Add(loading => loading.IsInline, true));

        Assert.Contains("loading-orbit-inline", cut.Find(".loading-orbit").ClassList);
        Assert.NotNull(cut.Find(".loading-orbit-ring").QuerySelector("svg.loading-orbit-icon"));
        Assert.Equal("Loading…", cut.Find(".visually-hidden").TextContent);
    }

    [Fact]
    public void The_pin_offers_the_opposite_of_what_is_already_true()
    {
        var cut = RenderComponent<PinButton>(parameters => parameters
            .Add(pin => pin.IsPinned, false)
            .Add(pin => pin.OnPinnedChanged, _ => { }));

        Assert.Equal("Pin to top", cut.Find("button").GetAttribute("title"));
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void A_pinned_thing_offers_to_be_unpinned()
    {
        var cut = RenderComponent<PinButton>(parameters => parameters
            .Add(pin => pin.IsPinned, true)
            .Add(pin => pin.OnPinnedChanged, _ => { }));

        Assert.Equal("Unpin", cut.Find("button").GetAttribute("title"));
        Assert.Contains("pin-button-pinned", cut.Find("button").ClassName);
    }

    [Fact]
    public void The_pin_reports_the_state_to_save_rather_than_the_one_it_had()
    {
        // Already flipped, so no caller has to remember to flip it again - and none can flip it twice.
        bool? reported = null;
        var cut = RenderComponent<PinButton>(parameters => parameters
            .Add(pin => pin.IsPinned, false)
            .Add(pin => pin.OnPinnedChanged, pinned => reported = pinned));

        cut.Find("button").Click();

        Assert.True(reported);
    }

    [Fact]
    public void A_pin_cannot_be_clicked_into_a_queue_of_pending_writes()
    {
        var cut = RenderComponent<PinButton>(parameters => parameters
            .Add(pin => pin.IsPinned, false)
            .Add(pin => pin.IsBusy, true)
            .Add(pin => pin.OnPinnedChanged, _ => { }));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void An_overflow_menu_keeps_its_entries_out_of_the_way_until_asked()
    {
        var cut = RenderComponent<OverflowMenu>(parameters => parameters
            .AddChildContent("<button class=\"avatar-dropdown-item\">Rename</button>"));

        Assert.Empty(cut.FindAll(".overflow-menu-dropdown"));
        Assert.Equal("false", cut.Find(".overflow-menu-trigger").GetAttribute("aria-expanded"));

        cut.Find(".overflow-menu-trigger").Click();

        Assert.Contains("Rename", cut.Find(".overflow-menu-dropdown").TextContent);
        Assert.Equal("true", cut.Find(".overflow-menu-trigger").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void A_menu_of_actions_shuts_behind_the_one_that_was_picked()
    {
        // A menu left open over the result of what it just started is in the way of reading it.
        var cut = RenderComponent<OverflowMenu>(parameters => parameters
            .AddChildContent("<button class=\"avatar-dropdown-item\">Rename</button>"));
        cut.Find(".overflow-menu-trigger").Click();

        cut.Find(".overflow-menu-dropdown button").Click();

        Assert.Empty(cut.FindAll(".overflow-menu-dropdown"));
    }

    [Fact]
    public void A_menu_of_settings_stays_open_so_two_can_be_changed()
    {
        var cut = RenderComponent<OverflowMenu>(parameters => parameters
            .Add(menu => menu.StaysOpen, true)
            .AddChildContent("<button class=\"avatar-dropdown-item\">Show completed</button>"));
        cut.Find(".overflow-menu-trigger").Click();

        cut.Find(".overflow-menu-dropdown button").Click();

        Assert.Single(cut.FindAll(".overflow-menu-dropdown"));
    }

    [Fact]
    public void A_settings_menu_can_still_be_shut_from_outside_it()
    {
        var cut = RenderComponent<OverflowMenu>(parameters => parameters
            .Add(menu => menu.StaysOpen, true)
            .AddChildContent("<button class=\"avatar-dropdown-item\">Show completed</button>"));
        cut.Find(".overflow-menu-trigger").Click();

        cut.InvokeAsync(cut.Instance.Close);

        Assert.Empty(cut.FindAll(".overflow-menu-dropdown"));
    }

    [Fact]
    public void A_locked_feature_says_what_is_missing_and_where_to_fix_it()
    {
        // An empty list here would be a lie: there may well be plenty to show once it is unlocked.
        var cut = RenderComponent<FeatureLocked>(parameters => parameters
            .Add(locked => locked.Explanation, "Conversations, with one person or with several."));

        Assert.Contains("Not unlocked yet", cut.Markup);
        Assert.Contains("Conversations, with one person or with several.", cut.Markup);
        Assert.Equal("/options", cut.Find("a").GetAttribute("href"));
        // Behind a "!" beside the title rather than as prose under it: it is true of this screen now.
        Assert.Equal("!", cut.Find(".options-row-title .field-hint-mark").TextContent);
        Assert.Contains("Conversations, with one person or with several.", cut.Find(".field-hint-bubble").TextContent);
    }

    [Theory]
    [InlineData(PresenceStatus.Available)]
    [InlineData(PresenceStatus.Away)]
    [InlineData(PresenceStatus.DoNotDisturb)]
    [InlineData(PresenceStatus.Offline)]
    public void Every_presence_a_person_can_be_in_is_drawn_as_something(PresenceStatus status)
    {
        // Including offline: a missing dot and a dot that says "not here" read differently.
        var cut = RenderComponent<PresenceDot>(parameters => parameters.Add(dot => dot.Status, status));

        Assert.NotEmpty(cut.Markup.Trim());
    }
}
