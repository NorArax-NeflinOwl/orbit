namespace Orbit.Mobile.Screens.Navigation;

/// <summary>
/// Where back leads: the screens the reader actually came through, in order.
///
/// This replaced <c>UpNavigation</c>, which answered the same question from a fixed map of parents - a
/// hierarchy rather than a history. That map was the right answer while every screen carried a bottom
/// rail with "Back to notes" written on it, because the button and the gesture then agreed. The
/// redesign takes the rail away and puts a single back arrow in the top bar, and an arrow that leads
/// somewhere other than where you came from is a trick: a note opened from the dashboard used to send
/// you to the notes list, which you had never seen.
///
/// The stack is deliberately shallow, and stays so without pruning:
///
/// <list type="bullet">
/// <item>A <b>root</b> - signing in, registering, the build the server has retired - clears everything.
/// There must be nothing at all behind those to be swiped past.</item>
/// <item>A <b>section</b> chosen from the drawer resets to the dashboard and itself, so hopping between
/// sections cannot grow the stack; the dashboard resets to only itself. This is what a drawer does
/// everywhere: its destinations are alternatives to each other, not steps into each other.</item>
/// <item>A <b>detail</b> pushes. Re-showing the screen already on top replaces it instead of stacking a
/// second copy, so opening a conversation from a conversation still leaves back pointing at the list.
/// </item>
/// </list>
///
/// The delegate is how a screen is shown again. It is supplied by whoever navigated rather than worked
/// out here, because most detail screens need an argument - which note, which conversation - and this
/// class has no way to name one.
/// </summary>
public sealed class ScreenHistory
{
    /// <summary>How a screen joins the history when it is shown.</summary>
    public enum Arrival
    {
        /// <summary>Clears everything behind it. Sign-in, register, and the screens before there is anywhere to go.</summary>
        Root,

        /// <summary>A destination in the drawer: resets to the dashboard and this.</summary>
        Section,

        /// <summary>A step into something: pushes.</summary>
        Detail
    }

    private readonly record struct Entry(Screen Screen, Action Show);

    private readonly List<Entry> _stack = [];

    /// <summary>
    /// Starts at the startup screen because that is what the window opens on, and the navigator is not
    /// what puts it there - see App.CreateWindow in Orbit.Maui.
    /// </summary>
    public Screen Current { get; private set; } = Screen.Startup;

    /// <summary>
    /// Whether there is anywhere to go back to, which is what decides between the menu and the back
    /// arrow in the top bar. Raised whenever it changes so the bar can redraw.
    /// </summary>
    public bool CanGoBack => _stack.Count > 1;

    /// <summary>Told when <see cref="CanGoBack"/> or <see cref="Current"/> changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Told by the navigator as it changes screens. <paramref name="show"/> is what will be run to
    /// bring this screen back, argument and all.
    /// </summary>
    public void Arrived(Screen screen, Arrival arrival, Action show)
    {
        var entry = new Entry(screen, show);

        switch (arrival)
        {
            case Arrival.Root:
                _stack.Clear();
                _stack.Add(entry);
                break;

            case Arrival.Section:
                // The dashboard is the floor everything else stands on, so it is kept rather than
                // re-run: re-running it here would show it and then immediately show the section over
                // the top, which is a visible flicker on the way to somewhere else.
                var dashboard = _stack.FirstOrDefault(e => e.Screen == Screen.Dashboard);
                _stack.Clear();
                if (screen != Screen.Dashboard && dashboard.Show is not null)
                {
                    _stack.Add(dashboard);
                }
                _stack.Add(entry);
                break;

            default:
                if (_stack.Count > 0 && _stack[^1].Screen == screen)
                {
                    _stack[^1] = entry;
                }
                else
                {
                    _stack.Add(entry);
                }
                break;
        }

        Current = screen;
        Changed?.Invoke();
    }

    /// <summary>
    /// What the screen on top wants asked before back takes it away - answering true to let it go and
    /// false to stay. Null on every screen that has nothing to lose, which is nearly all of them: the
    /// note editor sets one because it is written by Save and by nothing else, so leaving it with
    /// something typed would throw the typing away without a word.
    ///
    /// One at a time, and identified by the delegate itself when it is taken down, so a screen cannot
    /// clear a question that a later one put up.
    /// </summary>
    private Func<Task<bool>>? _mayLeave;

    /// <inheritdoc cref="_mayLeave"/>
    public void AskBeforeLeaving(Func<Task<bool>> ask) => _mayLeave = ask;

    /// <inheritdoc cref="_mayLeave"/>
    public void StopAskingBeforeLeaving(Func<Task<bool>> ask)
    {
        if (_mayLeave == ask)
        {
            _mayLeave = null;
        }
    }

    /// <summary>
    /// Goes back one screen. False when there is nothing behind this one, which the caller answers by
    /// letting the platform do what it would have done - on Android, leaving the app.
    ///
    /// True also means "this press has been dealt with" rather than "the screen has changed": where the
    /// screen on top has a question to ask first, back is answered here and the going happens later, if
    /// at all. The alternative is an async back press, which Android's own callback cannot wait for.
    /// </summary>
    public bool GoBack()
    {
        if (!CanGoBack)
        {
            return false;
        }

        if (_mayLeave is { } ask)
        {
            _ = LeaveIfAllowedAsync(ask);
            return true;
        }

        Pop();
        return true;
    }

    private async Task LeaveIfAllowedAsync(Func<Task<bool>> ask)
    {
        if (await ask() && CanGoBack)
        {
            Pop();
        }
    }

    private void Pop()
    {
        _stack.RemoveAt(_stack.Count - 1);
        var back = _stack[^1];

        // Popped before it is shown, because showing it calls Arrived again - which would otherwise
        // find this same screen already on top and replace it, leaving the stack one entry too long
        // for ever after.
        _stack.RemoveAt(_stack.Count - 1);
        back.Show();
    }
}
