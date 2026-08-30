namespace Orbit.Contracts.Notifications;

/// <param name="Title">
/// The English sentence with {0}-style holes in it, which is what both clients' dictionary is keyed by:
/// they render it in the reader's language. Usually a phrase with no holes at all - "New message" - and
/// then it is simply the sentence. See Orbit.Core's PushNotificationPayload for why the server sends
/// this rather than a finished sentence: the language is a preference each device keeps for itself, and
/// nothing ever tells the server about it.
/// </param>
/// <param name="TitleArguments">
/// What fills those holes. Never translated - a name, or the reader's own words for their own things.
/// Empty for an entry whose title says the same thing however it is read, which is most of them.
/// </param>
/// <param name="IsDismissed">
/// True once the reader has cleared this entry out of the panel. The notifications page still lists it,
/// marked as cleared, until the retention window deletes it - see NotificationEntry.Dismiss.
/// </param>
public sealed record NotificationEntryDto(
    Guid Id, string Kind, string Title, string Body, string? Url, DateTimeOffset CreatedAtUtc, bool IsRead,
    bool IsDismissed = false,
    IReadOnlyList<string>? TitleArguments = null,
    /// <inheritdoc cref="TitleArguments"/>
    IReadOnlyList<string>? BodyArguments = null);
