namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// How much of the calendar is on screen.
///
/// The phone's own, rather than shared with the browser: what each mode <i>draws</i> is nothing alike -
/// a phone picks a day out of the grid it is already showing, a browser has a sidebar to pick from - and
/// four names are not worth a shared type when none of the behaviour behind them is shared.
///
/// The week is the phone's own answer as well. It used to arrive by accident: the month grid shrank to
/// the week the reader was standing on as the list beneath it was scrolled past, so "one week" was a
/// thing that happened to you rather than a thing you asked for. It is one of the four now, and nothing
/// shrinks by itself.
/// </summary>
public enum CalendarViewMode
{
    Day,

    Week,

    Month,

    Year
}
