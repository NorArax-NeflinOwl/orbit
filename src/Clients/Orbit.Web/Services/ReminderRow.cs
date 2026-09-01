namespace Orbit.Web.Services;

/// <summary>
/// One reminder row in the event form: either a picked preset (see <see cref="Presets"/>) or a custom
/// amount and unit (mirrors Google Calendar's reminder picker) - either way it always resolves down to
/// <see cref="MinutesBeforeStart"/>, the only thing the API actually stores.
/// </summary>
public sealed class ReminderRow
{
    /// <summary>
    /// The lead times offered without anybody having to work out what a fortnight is in minutes.
    /// "When it starts" is here as well as being its own checkbox: as a reminder it is one of several
    /// times something can be said, and as a checkbox it is the answer to "and tell me when it begins".
    /// </summary>
    public static readonly (int Minutes, string Label)[] Presets =
    [
        (5, "5 minutes before"),
        (10, "10 minutes before"),
        (15, "15 minutes before"),
        (30, "30 minutes before"),
        (60, "1 hour before"),
        (120, "2 hours before"),
        (360, "6 hours before"),
        (720, "12 hours before"),
        (1440, "1 day before"),
        (2880, "2 days before"),
        (10080, "1 week before")
    ];

    public int MinutesBeforeStart { get; set; }
    public bool IsCustom { get; set; }
    public int CustomAmount { get; set; } = 10;
    public string CustomUnit { get; set; } = "minutes";

    /// <summary>Bound to the row's &lt;select&gt; - "custom" or the preset's minutes value as a string.</summary>
    public string PresetSelection
    {
        get => IsCustom ? "custom" : MinutesBeforeStart.ToString();
        set
        {
            if (value == "custom")
            {
                IsCustom = true;
                RecomputeFromCustom();
            }
            else if (int.TryParse(value, out var minutes))
            {
                IsCustom = false;
                MinutesBeforeStart = minutes;
            }
        }
    }

    public void RecomputeFromCustom()
    {
        var unitMinutes = CustomUnit switch
        {
            "weeks" => 10080,
            "days" => 1440,
            "hours" => 60,
            _ => 1
        };
        MinutesBeforeStart = Math.Max(1, CustomAmount) * unitMinutes;
    }

    /// <summary>Builds a row for minutes, matching it to a preset when one exists exactly, custom otherwise.</summary>
    public static ReminderRow FromMinutes(int minutes)
    {
        if (Presets.Any(preset => preset.Minutes == minutes))
        {
            return new ReminderRow { MinutesBeforeStart = minutes, IsCustom = false };
        }

        var (amount, unit) = minutes switch
        {
            > 0 and _ when minutes % 10080 == 0 => (minutes / 10080, "weeks"),
            > 0 and _ when minutes % 1440 == 0 => (minutes / 1440, "days"),
            > 0 and _ when minutes % 60 == 0 => (minutes / 60, "hours"),
            _ => (minutes, "minutes")
        };
        return new ReminderRow { MinutesBeforeStart = minutes, IsCustom = true, CustomAmount = amount, CustomUnit = unit };
    }
}
