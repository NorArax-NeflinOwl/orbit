namespace Orbit.Core.Notifications;

/// <summary>
/// Builds the subject and body of the e-mail telling someone something has been shared with them - the
/// mailed counterpart of the feed entry and the push <see cref="SharedItemNotifier"/> produces.
///
/// Written as whole sentences per kind for the same reason the feed entry is (see SharedItemNotifier):
/// a language that declines its nouns cannot have "a note" handed to it as a word to drop into a
/// common sentence.
/// </summary>
public static class SharedItemEmailContent
{
    public static (string Subject, string Body) Build(SharedItemKind kind, string sharerName, string? itemTitle)
    {
        var subject = SubjectFor(kind, sharerName);

        var bodyLines = new List<string> { $"{subject}." };
        if (!string.IsNullOrWhiteSpace(itemTitle))
        {
            bodyLines.Add($"Name: {itemTitle}");
        }

        bodyLines.Add(WhatHappensNext(kind));

        return (subject, string.Join(Environment.NewLine, bodyLines));
    }

    private static string SubjectFor(SharedItemKind kind, string sharerName) => kind switch
    {
        SharedItemKind.Note => $"{sharerName} shared a note with you",
        SharedItemKind.TaskList => $"{sharerName} shared a task list with you",
        SharedItemKind.CalendarEvent => $"{sharerName} shared an event with you",
        SharedItemKind.Warehouse => $"{sharerName} shared a warehouse with you",
        _ => $"{sharerName} shared their location with you"
    };

    /// <summary>
    /// Where to go for it, which is where the notification leads too - a shared position needs no
    /// accepting and is already on the map, everything else waits in the conversation with whoever
    /// sent it (see SharedItemNotifier.UrlFor).
    /// </summary>
    private static string WhatHappensNext(SharedItemKind kind)
        => kind == SharedItemKind.Location
            ? "It is on your map in Orbit."
            : "Open your conversation with them in Orbit to accept it.";
}
