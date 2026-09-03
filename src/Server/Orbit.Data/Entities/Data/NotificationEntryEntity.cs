namespace Orbit.Data.Entities;

/// <summary>See Orbit.Core.Notifications.NotificationEntry.</summary>
public sealed class NotificationEntryEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// What fills the {0}-style holes in Title and Body, as JSON - null for an entry with none, which
    /// is every entry written before the server stopped finishing these sentences for the clients.
    /// </summary>
    public string? TitleArguments { get; set; }

    /// <inheritdoc cref="TitleArguments"/>
    public string? BodyArguments { get; set; }
    public string? Url { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
    public DateTimeOffset? DismissedAtUtc { get; set; }
}
