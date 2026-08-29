using AngleSharp.Dom;
using Bunit;
using Orbit.Core.Users;
using Orbit.Web.Components;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The one list both chat screens show: people and groups together, so "who have I been talking to" is
/// answered in one place rather than two.
/// </summary>
public sealed class ConversationListTests : OrbitTestContext
{
    private static Conversation Person(string name, int unread = 0)
        => new(Guid.NewGuid(), name, IsGroup: false, unread, PresenceStatus.Available, unread > 0 ? $"{unread} new" : null);

    private static Conversation Group(string name)
        => new(Guid.NewGuid(), name, IsGroup: true, UnreadCount: 0, Presence: null, "3 members");

    private IRenderedComponent<ConversationList> Render(
        IReadOnlyList<Conversation> conversations, Guid? activeId = null, bool isCollapsed = false)
        => RenderComponent<ConversationList>(parameters => parameters
            .Add(list => list.Conversations, conversations)
            .Add(list => list.ActiveId, activeId)
            .Add(list => list.IsCollapsed, isCollapsed)
            .Add(list => list.OnSelected, _ => { }));

    private static IReadOnlyList<string> NamesIn(IRenderedComponent<ConversationList> cut)
        => [.. cut.FindAll(".chat-list-name").Select(name => name.TextContent.Trim())];

    [Fact]
    public void People_and_groups_are_one_list()
    {
        var cut = Render([Person("Anna"), Group("Weekend trip"), Person("Bartek")]);

        Assert.Equal(["Anna", "Weekend trip", "Bartek"], NamesIn(cut));
        Assert.Equal(3, cut.FindAll(".chat-list-item").Count);
    }

    [Fact]
    public void A_group_row_says_it_is_a_group()
    {
        // Both kinds share a list now, so a row has to say which kind it is.
        var cut = Render([Person("Anna"), Group("Weekend trip")]);

        var kinds = cut.FindAll(".chat-list-item").Select(row => row.QuerySelector(".conversation-kind") is not null);
        Assert.Equal([false, true], kinds);
    }

    [Fact]
    public void The_conversation_on_screen_is_marked_as_the_open_one()
    {
        var group = Group("Weekend trip");

        var cut = Render([Person("Anna"), group], activeId: group.Id);

        var active = Assert.Single(cut.FindAll(".chat-list-item.active"));
        Assert.Contains("Weekend trip", active.TextContent);
    }

    [Fact]
    public void What_is_waiting_to_be_read_is_shown_on_the_row()
    {
        var cut = Render([Person("Anna", unread: 3)]);

        Assert.Equal("3", cut.Find(".notif-badge").TextContent);
        Assert.Contains("unread", cut.Find(".chat-list-item").ClassName);
    }

    [Fact]
    public void A_long_wait_does_not_stretch_the_avatar()
    {
        var cut = Render([Person("Anna", unread: 42)]);

        Assert.Equal("9+", cut.Find(".notif-badge").TextContent);
    }

    [Fact]
    public void Folding_says_so_and_leaves_the_stylesheet_to_do_it()
    {
        // The names, the search and "New group" all stay in the markup: on a narrow screen this list is
        // a slide-out drawer, where folding has no meaning and the stylesheet hands them back. It used
        // to leave them out, and then no amount of CSS could bring them back - the drawer opened as a
        // wide panel of bare initials with no search and nobody's name on it.
        var cut = Render([Person("Anna"), Group("Weekend trip")], isCollapsed: true);

        Assert.Contains("collapsed", cut.Find(".chat-list").ClassName);
        Assert.Equal(["Anna", "Weekend trip"], NamesIn(cut));
        Assert.Single(cut.FindAll(".chat-list-search"));
        Assert.Equal(2, cut.FindAll(".chat-list-item").Count);
    }

    [Fact]
    public void Unfolded_it_says_that_too()
    {
        // The class is the whole difference, so it has to be absent as reliably as it is present.
        var cut = Render([Person("Anna")]);

        Assert.DoesNotContain("collapsed", cut.Find(".chat-list").ClassName);
    }

    [Fact]
    public void An_empty_list_says_so_whether_it_is_folded_or_not()
    {
        // Folded, the stylesheet hides this; in the drawer it is the only thing that explains the
        // emptiness. Left out of the markup, the drawer had nothing to say at all.
        var cut = Render([], isCollapsed: true);

        Assert.Contains("Nothing matches that.", cut.Find(".chat-list-empty").TextContent);
    }

    [Fact]
    public void Starting_a_group_is_offered_whether_it_is_folded_or_not()
    {
        var cut = RenderComponent<ConversationList>(parameters => parameters
            .Add(list => list.Conversations, [Person("Anna")])
            .Add(list => list.IsCollapsed, true)
            .Add(list => list.OnSelected, _ => { })
            .Add(list => list.OnNewGroup, () => { }));

        Assert.Single(cut.FindAll(".chat-list-new-group"));
    }

    [Fact]
    public void A_screen_that_cannot_start_a_group_is_not_offered_it()
    {
        // Unlike folding, this one really is absent: there is nothing behind the button to invoke.
        var cut = Render([Person("Anna")]);

        Assert.Empty(cut.FindAll(".chat-list-new-group"));
    }

    [Fact]
    public void Choosing_a_row_hands_back_which_conversation_it_was()
    {
        var group = Group("Weekend trip");
        Conversation? chosen = null;
        var cut = RenderComponent<ConversationList>(parameters => parameters
            .Add(list => list.Conversations, [Person("Anna"), group])
            .Add(list => list.OnSelected, conversation => chosen = conversation));

        cut.FindAll(".chat-list-item").ToArray()[1].Click();

        Assert.Equal(group, chosen);
    }

    [Fact]
    public void The_search_box_shows_what_has_been_typed_into_it()
    {
        // A page passing its own field has to see it back: the box once rendered the literal name of it.
        var cut = RenderComponent<ConversationList>(parameters => parameters
            .Add(list => list.Conversations, [Person("Anna")])
            .Add(list => list.OnSelected, _ => { })
            .Add(list => list.Search, "ann"));

        Assert.Equal("ann", cut.Find(".chat-list-search").GetAttribute("value"));
    }

    [Fact]
    public void Typing_in_the_search_box_is_handed_back_to_the_page()
    {
        string? typed = null;
        var cut = RenderComponent<ConversationList>(parameters => parameters
            .Add(list => list.Conversations, [Person("Anna")])
            .Add(list => list.OnSelected, _ => { })
            .Add(list => list.SearchChanged, value => typed = value));

        cut.Find(".chat-list-search").Input("we");

        Assert.Equal("we", typed);
    }

    [Fact]
    public void Nothing_matching_the_search_says_so_rather_than_showing_an_empty_panel()
    {
        var cut = Render([]);

        Assert.Contains("Nothing matches that.", cut.Markup);
    }

    [Fact]
    public void Starting_a_group_is_only_offered_where_the_screen_can_do_it()
    {
        var withoutIt = Render([Person("Anna")]);
        Assert.Empty(withoutIt.FindAll(".chat-list-new-group"));

        var withIt = RenderComponent<ConversationList>(parameters => parameters
            .Add(list => list.Conversations, [Person("Anna")])
            .Add(list => list.OnSelected, _ => { })
            .Add(list => list.OnNewGroup, () => { }));
        Assert.Single(withIt.FindAll(".chat-list-new-group"));
    }
}
