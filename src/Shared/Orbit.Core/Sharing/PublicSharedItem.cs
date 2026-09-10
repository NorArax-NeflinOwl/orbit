namespace Orbit.Core.Sharing;

/// <summary>
/// What a public link actually shows: one flat, readable shape covering every kind of item, so the page
/// behind a link renders a note, a task list, an event, an inventory and a place without a separate view
/// of a separate model for each.
///
/// Deliberately a projection rather than the item itself. A reader with a link is not a user of this
/// account, so they see what the owner meant to show and nothing incidental - no ids, no share history,
/// no lock state, no notification settings, and no e-mail address for anyone involved.
/// </summary>
/// <param name="OwnerDisplayName">Whose item this is, so a reader knows who sent them the link - the display name only, never the e-mail behind it.</param>
public sealed record PublicSharedItem(
    SharedItemType ItemType,
    string Title,
    string? Subtitle,
    IReadOnlyList<PublicSharedItemLine> Lines,
    string OwnerDisplayName,
    DateTimeOffset UpdatedAtUtc);

/// <param name="Detail">A due date, a quantity, a location - whatever the line's own kind adds beneath the text.</param>
/// <param name="IsFailed">Crossed out rather than ticked - a line, or an entry, somebody gave up on.</param>
public sealed record PublicSharedItemLine(string Text, bool IsChecklistItem, bool IsChecked, string? Detail, bool IsFailed = false);
