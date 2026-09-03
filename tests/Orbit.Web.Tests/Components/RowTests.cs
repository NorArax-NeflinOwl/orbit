using Bunit;
using Orbit.Web.Components;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// A row draws as a button when pressing it does something, and as a plain div when it doesn't - the
/// same "list-row" either way, so nothing about how it looks says which one a reader got.
/// </summary>
public sealed class RowTests : OrbitTestContext
{
    [Fact]
    public void With_a_press_handler_it_draws_as_a_button_and_pressing_it_calls_the_handler()
    {
        var pressed = false;
        var cut = RenderComponent<Row>(parameters => parameters
            .Add(row => row.Title, "Milk")
            .Add(row => row.OnPressed, () => pressed = true));

        var button = cut.Find("button.list-row.list-row-button");
        button.Click();

        Assert.True(pressed);
        Assert.Contains("Milk", cut.Find(".row-title").TextContent);
    }

    [Fact]
    public void Without_a_press_handler_it_draws_as_a_plain_div_with_no_button()
    {
        var cut = RenderComponent<Row>(parameters => parameters
            .Add(row => row.Title, "Milk"));

        Assert.Empty(cut.FindAll("button"));
        var div = cut.Find("div.list-row");
        Assert.Contains("Milk", div.TextContent);
    }

    [Fact]
    public void Leading_and_trailing_content_render_when_given_and_are_absent_when_not()
    {
        var cutWith = RenderComponent<Row>(parameters => parameters
            .Add(row => row.Title, "Milk")
            .Add(row => row.Leading, "<span class=\"avatar-sm\">M</span>")
            .Add(row => row.Trailing, "<span class=\"row-meta\">2</span>"));

        Assert.NotEmpty(cutWith.FindAll(".avatar-sm"));
        Assert.NotEmpty(cutWith.FindAll(".row-meta"));

        var cutWithout = RenderComponent<Row>(parameters => parameters
            .Add(row => row.Title, "Milk"));

        Assert.Empty(cutWithout.FindAll(".avatar-sm"));
        Assert.Empty(cutWithout.FindAll(".row-meta"));
    }

    [Fact]
    public void A_page_can_add_its_own_class_beside_the_shared_one()
    {
        var cut = RenderComponent<Row>(parameters => parameters
            .Add(row => row.Title, "Milk")
            .Add(row => row.OnPressed, () => { })
            .Add(row => row.AdditionalClass, "unread"));

        var classes = cut.Find("button").ClassName;
        Assert.Contains("list-row", classes);
        Assert.Contains("list-row-button", classes);
        Assert.Contains("unread", classes);
    }
}
