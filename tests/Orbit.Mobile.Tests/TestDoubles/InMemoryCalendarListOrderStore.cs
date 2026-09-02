using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>What order the calendar's list is read in, kept for as long as one test runs.</summary>
internal sealed class InMemoryCalendarListOrderStore : ICalendarListOrderStore
{
    private CalendarListSortOrder _sortOrder = CalendarListSortOrder.When;

    public CalendarListSortOrder Read() => _sortOrder;

    public void Write(CalendarListSortOrder sortOrder) => _sortOrder = sortOrder;
}
