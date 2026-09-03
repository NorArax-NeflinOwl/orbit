using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>How the calendar's list is read, kept for as long as one test runs.</summary>
internal sealed class InMemoryCalendarListOrderStore : ICalendarListOrderStore
{
    private CalendarListReading _reading = CalendarListReading.Default;

    public CalendarListReading Read() => _reading;

    public void Write(CalendarListReading reading) => _reading = reading;

    /// <summary>What a test can set beforehand, and read back to see what the screen wrote.</summary>
    public bool ShowsEverything
    {
        get => _reading.ShowsEverything;
        set => _reading = _reading with { ShowsEverything = value };
    }
}
