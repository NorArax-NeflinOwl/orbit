namespace Orbit.Contracts.Notifications;

/// <summary>Url is the client-side route the reader has just arrived at, e.g. "/tasks/{id}".</summary>
public sealed record MarkNotificationsReadAtUrlRequest(string Url);
