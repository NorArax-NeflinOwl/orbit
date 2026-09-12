using Bunit;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The three-way question every list on Orbit asks before it draws anything: not loaded yet, loaded
/// and genuinely empty, or here it is. What "here it is" draws is entirely the caller's own, so that is
/// not this component's to test - see Notes/Tasks/InventoriesTests for that.
/// </summary>
public sealed class ObjectListTests : OrbitTestContext
{
    [Fact]
    public void Loading_says_so_and_draws_nothing_else()
    {
        var cut = RenderComponent<ObjectList>(parameters => parameters
            .Add(list => list.IsLoading, true)
            .Add(list => list.EmptyMessage, "No notes.")
            .AddChildContent("<div class=\"item-card-list\">Cards</div>"));

        Assert.Contains("Loading", cut.Markup);
        Assert.DoesNotContain("Cards", cut.Markup);
    }

    [Fact]
    public void Empty_says_so_and_draws_nothing_else()
    {
        var cut = RenderComponent<ObjectList>(parameters => parameters
            .Add(list => list.IsEmpty, true)
            .Add(list => list.EmptyMessage, "No notes.")
            .AddChildContent("<div class=\"item-card-list\">Cards</div>"));

        Assert.Contains("No notes.", cut.Find(".empty-hint").TextContent);
        Assert.DoesNotContain("Cards", cut.Markup);
    }

    /// <summary>
    /// The message stands where the list would be, so it is centred there like the list's own contents
    /// would be - see .empty-list, which is what tells it apart from an .empty-hint that is a note.
    /// </summary>
    [Fact]
    public void The_empty_message_is_drawn_as_the_list_standing_empty()
    {
        var cut = RenderComponent<ObjectList>(parameters => parameters
            .Add(list => list.IsEmpty, true)
            .Add(list => list.EmptyMessage, "No notes."));

        Assert.Contains("empty-list", cut.Find(".empty-hint").ClassList);
    }

    /// <summary>Loaded and non-empty is everything else - the caller's own content, whatever shape it takes.</summary>
    [Fact]
    public void Loaded_and_not_empty_draws_the_callers_own_content()
    {
        var cut = RenderComponent<ObjectList>(parameters => parameters
            .Add(list => list.EmptyMessage, "No notes.")
            .AddChildContent("<div class=\"item-card-list\">Cards</div>"));

        Assert.Contains("Cards", cut.Markup);
        Assert.Empty(cut.FindAll(".empty-hint"));
    }
}
