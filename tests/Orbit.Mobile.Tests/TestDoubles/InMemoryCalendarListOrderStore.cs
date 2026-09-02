using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>How the calendar's list is read, kept for as long as one test runs.</summary>
internal sealed class InMemoryCalendarListOrderStore : ICalendarListOrderStore
{
    private CalendarListSortOrder _sortOrder = CalendarListSortOrder.When;

    public CalendarListSortOrder Read() => _sortOrder;

    public void Write(CalendarListSortOrder sortOrder) => _sortOrder = sortOrder;

    public bool ReadShowsEverything() => ShowsEverything;

    public void WriteShowsEverything(bool showsEverything) => ShowsEverything = showsEverything;

    /// <summary>What a test can set beforehand, and read back to see what the screen wrote.</summary>
    public bool ShowsEverything { get; set; }
}
