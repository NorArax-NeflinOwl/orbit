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
