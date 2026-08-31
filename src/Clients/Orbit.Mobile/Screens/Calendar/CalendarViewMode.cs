namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// How much of the calendar is on screen. The same three the browser offers - see Orbit.Web's
/// Calendar.razor - so somebody who knows one knows the other.
///
/// The phone's own, rather than shared with the browser: what each mode <i>draws</i> is nothing alike -
/// a phone picks a day out of the grid it is already showing, a browser has a sidebar to pick from - and
/// three names are not worth a shared type when none of the behaviour behind them is shared.
/// </summary>
public enum CalendarViewMode
{
    Day,

    Month,

    Year
}
