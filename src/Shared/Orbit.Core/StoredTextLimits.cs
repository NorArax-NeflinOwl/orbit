using Orbit.Core.Abstractions;

namespace Orbit.Core;

/// <summary>
/// How long each kind of text Orbit stores may be, and the one way to refuse text that is longer.
///
/// These numbers exist twice by nature - once as a rule the domain enforces, once as a column width in
/// the database - so they are written down once here and read from both places (see OrbitDbContext,
/// which passes them to HasMaxLength). Two independent copies would drift, and the way that drift shows
/// up is the reason this file exists: until it did, every one of these limits was enforced only by
/// Postgres, so a title one character too long left the database to raise "value too long for type
/// character varying(200)" and the caller met an unexplained 500 instead of being told the rule.
/// </summary>
public static class StoredTextLimits
{
    /// <summary>What a note, a task list, an event, an inventory or a shelf item is called.</summary>
    public const int Title = 200;

    /// <summary>What somebody chose to be called - the same length as anything else with a name.</summary>
    public const int DisplayName = 200;

    /// <summary>The login somebody is found by. Short on purpose: it is typed by other people.</summary>
    public const int UserName = 64;

    /// <summary>
    /// What one entry on a task list says. Was 500 when an entry was a line typed into a single-line
    /// box; it is written in a text area now, where a paragraph is a reasonable thing to write, so it
    /// gets the same room an event's description has.
    /// </summary>
    public const int TaskDescription = 2000;

    /// <summary>What an event is about, at length - the one field meant to hold more than a line.</summary>
    public const int EventDescription = 2000;

    /// <summary>A place, written out: a street address is longer than a name but not unbounded.</summary>
    public const int Address = 300;

    /// <summary>A chat group's name. Shorter than other names because it is shown in a list beside faces.</summary>
    public const int GroupName = 120;

    /// <summary>How a shelf item is classified - what it is, and what it is kept with.</summary>
    public const int ProductType = 100;
    public const int Category = 100;

    /// <summary>A colour as it travels: "#rrggbb" and room for the other notations a picker might send.</summary>
    public const int Color = 20;

    /// <summary>
    /// Hands back <paramref name="value"/> when it fits, and refuses it when it does not.
    ///
    /// <paramref name="whatItIs"/> is read by whoever typed the text, so it names the field the way the
    /// screen does ("note's title", not "Title") - the message is the only thing telling them which of
    /// several boxes to shorten.
    /// </summary>
    public static string OrRefuse(string value, int limit, string whatItIs)
        => value.Length <= limit
            ? value
            : throw new InvalidRequestException($"A {whatItIs} can be at most {limit} characters, and this one is {value.Length}.");

    /// <summary>The same, for text that is allowed to be absent altogether.</summary>
    public static string? OrRefuseIfPresent(string? value, int limit, string whatItIs)
        => value is null ? null : OrRefuse(value, limit, whatItIs);
}
