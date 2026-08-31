namespace Orbit.Core.Inventory;

/// <summary>What a shelf item's expiry is counted in. None means it does not expire.</summary>
public enum ExpiryUnit
{
    None,
    Days,
    Weeks,
    Months,
    Years
}

/// <summary>
/// How long something keeps, rather than the day it stops keeping.
///
/// A date is still what gets stored - the expiry reminder needs one, and "in two weeks" is not a thing
/// a background service can compare against. What this expresses is only the asking: nobody stocking a
/// shelf knows a carton runs out on the 14th, they know it keeps a fortnight.
///
/// Shared by both clients rather than written twice. The rule has real content - which unit a date
/// reads back as, and what a date already gone reads as - and two copies of it would let a phone and a
/// browser disagree about what the same stored date means.
/// </summary>
public readonly record struct ExpiryPeriod(int Amount, ExpiryUnit Unit)
{
    /// <summary>What the boxes show for something that does not expire.</summary>
    public static ExpiryPeriod None { get; } = new(1, ExpiryUnit.None);

    /// <summary>How far ahead the units above are worth looking - past this, the coarser one says it.</summary>
    private const int MostYears = 20;
    private const int MostMonths = 24;

    /// <summary>
    /// Reads a stored date back as a length, choosing the coarsest unit that lands on it exactly - 14
    /// days reads as "2 weeks", 15 as "15 days". A date already gone reads as one day, because zero
    /// would offer nothing to edit.
    ///
    /// Months and years are asked of the calendar rather than divided out of a day count, because that
    /// is how <see cref="On"/> writes them: three months from the 30th of August is the 30th of
    /// November, which is 92 days, and 92 divides by neither 30 nor 7 - so a length somebody set in
    /// months read back to them as "92 days".
    /// </summary>
    public static ExpiryPeriod For(DateTimeOffset? expiresOn, DateTime today)
    {
        if (expiresOn is not { } expiry)
        {
            return None;
        }

        var landsOn = expiry.Date;
        if (landsOn <= today.Date)
        {
            return new(1, ExpiryUnit.Days);
        }

        for (var years = 1; years <= MostYears; years++)
        {
            if (today.Date.AddYears(years) == landsOn)
            {
                return new(years, ExpiryUnit.Years);
            }
        }

        for (var months = 1; months <= MostMonths; months++)
        {
            if (today.Date.AddMonths(months) == landsOn)
            {
                return new(months, ExpiryUnit.Months);
            }
        }

        var days = (int)(landsOn - today.Date).TotalDays;
        return days % 7 == 0 ? new(days / 7, ExpiryUnit.Weeks) : new(days, ExpiryUnit.Days);
    }

    /// <summary>
    /// The date this length lands on, counted from <paramref name="today"/> rather than from whatever
    /// the item held before: "keeps two weeks" is a statement about something just put on the shelf,
    /// which is when anybody sets it. Null for something that does not expire.
    /// </summary>
    public DateTimeOffset? On(DateTime today)
    {
        var midnight = new DateTimeOffset(today.Date, TimeZoneInfo.Local.GetUtcOffset(today.Date));
        return Unit switch
        {
            ExpiryUnit.Days => midnight.AddDays(Amount),
            ExpiryUnit.Weeks => midnight.AddDays(7 * Amount),
            ExpiryUnit.Months => midnight.AddMonths(Amount),
            ExpiryUnit.Years => midnight.AddYears(Amount),
            _ => null
        };
    }
}
