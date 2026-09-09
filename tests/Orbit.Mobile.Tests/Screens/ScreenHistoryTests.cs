using Orbit.Mobile.Screens.Navigation;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Where back leads. Both the phone's gesture and the top bar's arrow read this, and they are asked
/// from every screen - so the two things it can get wrong are everywhere at once: sending the reader
/// somewhere they have not been, or leaving the app when there was somewhere to go.
///
/// This replaced <c>UpNavigationTests</c>, which pinned down a fixed map of parents. The rules are
/// different now (a history, not a hierarchy), so the cases are too.
/// </summary>
public sealed class ScreenHistoryTests
{
    /// <summary>Records which screens were re-shown, in order, so a pop can be checked by its effect.</summary>
    private sealed class Trail
    {
        private readonly ScreenHistory _history;

        public Trail(ScreenHistory history) => _history = history;

        public List<Screen> Shown { get; } = [];

        public void Go(Screen screen, ScreenHistory.Arrival arrival)
            => _history.Arrived(screen, arrival, () =>
            {
                Shown.Add(screen);
                Go(screen, arrival);
            });
    }

    private static (ScreenHistory History, Trail Trail) Fresh()
    {
        var history = new ScreenHistory();
        return (history, new Trail(history));
    }

    /// <summary>
    /// A screen with something to lose is asked first - the note editor, which is written by Save and
    /// by nothing else. Answering no leaves the reader where they were.
    ///
    /// GoBack still says true, because true means "this press has been dealt with" rather than "the
    /// screen has changed": Android's back callback cannot wait for an answer, so the question is asked
    /// after it returns.
    /// </summary>
    [Fact]
    public async Task A_screen_that_asks_before_leaving_stays_put_when_the_answer_is_no()
    {
        var (history, trail) = Fresh();
        trail.Go(Screen.Notes, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Note, ScreenHistory.Arrival.Detail);
        var asked = 0;
        history.AskBeforeLeaving(() => { asked++; return Task.FromResult(false); });

        Assert.True(history.GoBack());
        await Task.Yield();

        Assert.Equal(1, asked);
        Assert.Empty(trail.Shown);
        Assert.True(history.CanGoBack);
    }

    [Fact]
    public async Task A_screen_that_asks_before_leaving_goes_when_the_answer_is_yes()
    {
        var (history, trail) = Fresh();
        trail.Go(Screen.Notes, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Note, ScreenHistory.Arrival.Detail);
        history.AskBeforeLeaving(() => Task.FromResult(true));

        Assert.True(history.GoBack());
        await Task.Yield();

        Assert.Equal([Screen.Notes], trail.Shown);
    }

    /// <summary>
    /// The question comes down with the screen that put it up, so the next screen is not asked what the
    /// last one wanted to know - and a screen cannot take down a question a later one put up.
    /// </summary>
    [Fact]
    public async Task A_question_is_only_taken_down_by_whoever_put_it_up()
    {
        var (history, trail) = Fresh();
        trail.Go(Screen.Notes, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Note, ScreenHistory.Arrival.Detail);
        Func<Task<bool>> theNote = () => Task.FromResult(false);
        Func<Task<bool>> somebodyElse = () => Task.FromResult(false);

        history.AskBeforeLeaving(theNote);
        history.StopAskingBeforeLeaving(somebodyElse);
        Assert.True(history.GoBack());
        await Task.Yield();
        Assert.Empty(trail.Shown);

        history.StopAskingBeforeLeaving(theNote);
        Assert.True(history.GoBack());
        await Task.Yield();
        Assert.Equal([Screen.Notes], trail.Shown);
    }

    [Fact]
    public void A_detail_goes_back_to_wherever_it_was_opened_from()
    {
        var (history, trail) = Fresh();
        trail.Go(Screen.Dashboard, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Note, ScreenHistory.Arrival.Detail);

        Assert.True(history.CanGoBack);
        Assert.True(history.GoBack());
        Assert.Equal([Screen.Dashboard], trail.Shown);
    }

    /// <summary>
    /// The whole reason the fixed map went. A note opened from the dashboard used to send the reader to
    /// the notes list on the way back - a screen they had never seen.
    /// </summary>
    [Fact]
    public void The_same_screen_goes_back_to_different_places_depending_on_the_way_in()
    {
        var (fromList, viaList) = Fresh();
        viaList.Go(Screen.Dashboard, ScreenHistory.Arrival.Section);
        viaList.Go(Screen.Notes, ScreenHistory.Arrival.Section);
        viaList.Go(Screen.Note, ScreenHistory.Arrival.Detail);
        fromList.GoBack();

        var (fromDashboard, viaDashboard) = Fresh();
        viaDashboard.Go(Screen.Dashboard, ScreenHistory.Arrival.Section);
        viaDashboard.Go(Screen.Note, ScreenHistory.Arrival.Detail);
        fromDashboard.GoBack();

        Assert.Equal([Screen.Notes], viaList.Shown);
        Assert.Equal([Screen.Dashboard], viaDashboard.Shown);
    }

    [Fact]
    public void Details_stack_and_unwind_one_at_a_time()
    {
        var (history, trail) = Fresh();
        trail.Go(Screen.Dashboard, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Tasks, ScreenHistory.Arrival.Section);
        trail.Go(Screen.TaskList, ScreenHistory.Arrival.Detail);
        trail.Go(Screen.TaskItem, ScreenHistory.Arrival.Detail);

        Assert.True(history.GoBack());
        Assert.True(history.GoBack());
        Assert.True(history.GoBack());
        Assert.False(history.GoBack());
        Assert.Equal([Screen.TaskList, Screen.Tasks, Screen.Dashboard], trail.Shown);
    }

    /// <summary>
    /// A drawer's destinations are alternatives to each other rather than steps into each other, so
    /// hopping between them cannot grow the stack. Without this, a reader wandering the sections would
    /// have to press back once per section they had ever opened.
    /// </summary>
    [Fact]
    public void Hopping_between_sections_does_not_pile_them_up()
    {
        var (history, trail) = Fresh();
        trail.Go(Screen.Dashboard, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Notes, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Tasks, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Calendar, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Map, ScreenHistory.Arrival.Section);

        Assert.True(history.GoBack());
        Assert.False(history.GoBack());
        Assert.Equal([Screen.Dashboard], trail.Shown);
    }

    [Fact]
    public void The_dashboard_is_the_floor()
    {
        var (history, trail) = Fresh();
        trail.Go(Screen.Dashboard, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Notes, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Dashboard, ScreenHistory.Arrival.Section);

        Assert.False(history.CanGoBack);
        Assert.False(history.GoBack());
        Assert.Empty(trail.Shown);
    }

    /// <summary>
    /// Signing out must leave nothing at all behind the sign-in screen, and signing in must not leave
    /// the sign-in screen behind the dashboard.
    /// </summary>
    [Fact]
    public void A_root_clears_everything_behind_it()
    {
        var (history, trail) = Fresh();
        trail.Go(Screen.Dashboard, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Notes, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Note, ScreenHistory.Arrival.Detail);
        trail.Go(Screen.SignIn, ScreenHistory.Arrival.Root);

        Assert.False(history.CanGoBack);
        Assert.False(history.GoBack());
        Assert.Empty(trail.Shown);

        trail.Go(Screen.Dashboard, ScreenHistory.Arrival.Section);
        Assert.False(history.CanGoBack);
    }

    /// <summary>Registering is a step off the sign-in screen, so back returns to it.</summary>
    [Fact]
    public void Registering_goes_back_to_signing_in()
    {
        var (history, trail) = Fresh();
        trail.Go(Screen.SignIn, ScreenHistory.Arrival.Root);
        trail.Go(Screen.Register, ScreenHistory.Arrival.Detail);

        Assert.True(history.GoBack());
        Assert.Equal([Screen.SignIn], trail.Shown);
    }

    /// <summary>
    /// Opening a conversation from a conversation - through a forwarded message, say - must not stack a
    /// second copy, or back would walk sideways through the thread instead of out to the list.
    /// </summary>
    [Fact]
    public void Re_showing_the_screen_on_top_replaces_it()
    {
        var (history, trail) = Fresh();
        trail.Go(Screen.Dashboard, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Contacts, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Conversation, ScreenHistory.Arrival.Detail);
        trail.Go(Screen.Conversation, ScreenHistory.Arrival.Detail);

        // Out to the list in one press, not two - and then on out to the dashboard, which is where
        // Contacts itself came from.
        Assert.True(history.GoBack());
        Assert.True(history.GoBack());
        Assert.False(history.GoBack());
        Assert.Equal([Screen.Contacts, Screen.Dashboard], trail.Shown);
    }

    /// <summary>
    /// The app opens on the startup screen without the navigator putting it there - so a build the
    /// server has retired, which never leaves that screen, has nothing to be swiped past.
    /// </summary>
    [Fact]
    public void Before_anything_has_navigated_there_is_nowhere_to_go()
    {
        var history = new ScreenHistory();

        Assert.Equal(Screen.Startup, history.Current);
        Assert.False(history.CanGoBack);
        Assert.False(history.GoBack());
    }

    [Fact]
    public void Arriving_says_where_the_reader_now_is()
    {
        var (history, trail) = Fresh();
        trail.Go(Screen.Dashboard, ScreenHistory.Arrival.Section);
        Assert.Equal(Screen.Dashboard, history.Current);

        trail.Go(Screen.Note, ScreenHistory.Arrival.Detail);
        Assert.Equal(Screen.Note, history.Current);
    }

    /// <summary>The bar redraws off this, so a move nobody announced would leave the wrong control in it.</summary>
    [Fact]
    public void Every_move_is_announced()
    {
        var (history, trail) = Fresh();
        var announced = 0;
        history.Changed += () => announced++;

        trail.Go(Screen.Dashboard, ScreenHistory.Arrival.Section);
        trail.Go(Screen.Note, ScreenHistory.Arrival.Detail);
        history.GoBack();

        Assert.Equal(3, announced);
    }
}
