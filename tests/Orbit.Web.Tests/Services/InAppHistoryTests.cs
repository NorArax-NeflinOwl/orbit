using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// The rules behind finishing a screen without leaving it in the browser's history - see InAppHistory.
/// What matters most is the direction every guess errs in: stepping back is the one move that could
/// leave Orbit, so it is only made onto an entry the trail has seen, and anything it cannot account for
/// makes it believe there is less history rather than more.
/// </summary>
public sealed class InAppHistoryTests
{
    [Fact]
    public void It_starts_with_the_page_the_app_was_opened_on()
        => Assert.Equal(["/notes"], new InAppHistory("/notes").Entries);

    [Fact]
    public void A_page_arrived_at_is_added_on_top()
    {
        var history = new InAppHistory("/notes");

        history.Arrived("/notes/1");

        Assert.Equal(["/notes", "/notes/1"], history.Entries);
    }

    /// <summary>The browser's own Back, which nothing announces: it is read off where the browser arrives.</summary>
    [Fact]
    public void Arriving_at_the_entry_just_before_is_read_as_Back()
    {
        var history = new InAppHistory("/notes");
        history.Arrived("/notes/1");

        history.Arrived("/notes");

        Assert.Equal(["/notes"], history.Entries);
    }

    [Fact]
    public void Finishing_onto_the_entry_just_before_steps_back_to_it()
    {
        var history = new InAppHistory("/notes");
        history.Arrived("/notes/new");

        var step = history.Leave("/notes");
        history.Arrived("/notes");

        Assert.Equal(new FinishStep(1, ReplaceWith: null), step);
        Assert.Equal(["/notes"], history.Entries);
    }

    /// <summary>
    /// A summary opened from the dashboard, and its form opened from the summary: saving the form returns
    /// to the summary by stepping back, so Back from there is the dashboard rather than the form again.
    /// </summary>
    [Fact]
    public void A_form_opened_from_a_summary_steps_back_onto_the_summary()
    {
        var history = new InAppHistory("/");
        history.Arrived("/notes/1?returnTo=%2F");
        history.Arrived("/notes/1/edit?returnTo=%2Fnotes%2F1%3FreturnTo%3D%252F");

        var step = history.Leave("/notes/1?returnTo=%2F");

        Assert.Equal(new FinishStep(1, ReplaceWith: null), step);
    }

    /// <summary>
    /// Typed in, reloaded, or reached from another site: there is nothing of Orbit's behind it, and
    /// stepping back would leave Orbit altogether. The page is replaced instead.
    /// </summary>
    [Fact]
    public void A_page_with_nothing_behind_it_is_replaced()
    {
        var history = new InAppHistory("/notes/1/edit");

        var step = history.Leave("/notes/1");
        history.Arrived("/notes/1");

        Assert.Equal(new FinishStep(0, "/notes/1"), step);
        Assert.Equal(["/notes/1"], history.Entries);
    }

    [Fact]
    public void A_destination_that_is_not_just_behind_replaces_the_page()
    {
        var history = new InAppHistory("/calendar");
        history.Arrived("/notes/1/edit");

        var step = history.Leave("/notes");
        history.Arrived("/notes");

        Assert.Equal(new FinishStep(0, "/notes"), step);
        Assert.Equal(["/calendar", "/notes"], history.Entries);
    }

    /// <summary>
    /// "Back to the calendar" means the calendar the reader left, on the day they were looking at - so the
    /// same page with more on its address counts. Not the other way round: a destination that names a
    /// query is a particular view, and a bare page is not it.
    /// </summary>
    [Fact]
    public void The_same_page_with_more_on_its_address_is_where_a_bare_destination_leads()
    {
        var history = new InAppHistory("/calendar?view=day&on=2026-09-14");
        history.Arrived("/calendar/1");

        Assert.Equal(new FinishStep(1, ReplaceWith: null), history.Leave("/calendar"));
    }

    [Fact]
    public void A_destination_naming_a_query_is_not_reached_by_a_bare_page()
    {
        var history = new InAppHistory("/calendar");
        history.Arrived("/calendar/1");

        Assert.Equal(new FinishStep(0, "/calendar?view=day"), history.Leave("/calendar?view=day"));
    }

    /// <summary>
    /// Deleting from a form opened on the thing's own page: both pages are left, since each would now
    /// open on "no longer exists". The summary is stepped back onto and replaced with the list.
    /// </summary>
    [Fact]
    public void Deleting_leaves_every_page_of_the_deleted_thing()
    {
        var history = new InAppHistory("/");
        history.Arrived("/notes/1?returnTo=%2F");
        history.Arrived("/notes/1/edit?returnTo=%2Fnotes%2F1");

        var step = history.Leave("/notes", leavingEverythingUnder: "/notes/1");
        var thenReplaceWith = history.Arrived("/notes/1?returnTo=%2F");
        history.Arrived("/notes");

        Assert.Equal(new FinishStep(1, "/notes"), step);
        Assert.Equal("/notes", thenReplaceWith);
        Assert.Equal(["/", "/notes"], history.Entries);
    }

    [Fact]
    public void Deleting_with_the_list_just_beneath_steps_back_past_both_pages()
    {
        var history = new InAppHistory("/notes");
        history.Arrived("/notes/1");
        history.Arrived("/notes/1/edit");

        var step = history.Leave("/notes", leavingEverythingUnder: "/notes/1");
        history.Arrived("/notes");

        Assert.Equal(new FinishStep(2, ReplaceWith: null), step);
        Assert.Equal(["/notes"], history.Entries);
    }

    /// <summary>
    /// A finish that never arrived - something else happened first - is not taken for having happened:
    /// whatever did arrive is read by the ordinary rules.
    /// </summary>
    [Fact]
    public void An_arrival_other_than_the_one_asked_for_is_read_as_a_new_page()
    {
        var history = new InAppHistory("/notes");
        history.Arrived("/notes/1/edit");
        history.Leave("/notes/1");

        history.Arrived("/calendar");

        Assert.Equal(["/notes", "/notes/1/edit", "/calendar"], history.Entries);
    }

    [Theory]
    [InlineData("/notes/1", true)]
    [InlineData("/notes/1/edit", true)]
    [InlineData("/notes/1?returnTo=%2F", true)]
    [InlineData("/notes/12", false)]
    [InlineData("/notes", false)]
    public void What_lies_under_a_deleted_thing(string location, bool isUnder)
        => Assert.Equal(isUnder, InAppHistory.IsUnder(location, "/notes/1"));
}
