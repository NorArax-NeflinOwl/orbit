namespace Orbit.Data.Entities;

/// <summary>
/// The colour one of an account's tags is drawn in - see Orbit.Core.Tags.TagColour. One row per tag per
/// account, keyed by the tag as TagNames.KeyOf folds it, so "Work" and "work" share one colour. An account
/// setting beside OS_NOTIFICATIONS_SETTINGS rather than a column on the notes and lists it colours: the
/// colour belongs to the word, whichever items carry it.
///
/// Readable by design, and said so in info/functionality.md: the tag is named here in the clear, so a tag
/// coloured while it was used only on private items is readable in this table even though every item
/// carrying it is sealed. Only a colour somebody chose puts a row here - nothing writes one on its own.
/// </summary>
public sealed class TagColourEntity
{
    public Guid UserId { get; set; }

    /// <summary>The tag as TagNames.KeyOf folds it - the half of the key that is not the account.</summary>
    public string NormalizedTag { get; set; } = string.Empty;

    /// <summary>The tag as it was written when its colour was last set, for showing.</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>A CSS colour, "#rrggbb" in lower case.</summary>
    public string Colour { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
