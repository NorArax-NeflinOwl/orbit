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
    public void Collapsed_to_initials_there_are_no_names_to_read()
    {
        var cut = Render([Person("Anna"), Group("Weekend trip")], isCollapsed: true);

        Assert.Empty(NamesIn(cut));
        Assert.Equal(2, cut.FindAll(".chat-list-item").Count);
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
