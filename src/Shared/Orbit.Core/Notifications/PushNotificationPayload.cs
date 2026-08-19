namespace Orbit.Core.Notifications;

/// <summary>
/// The content of a single push notification, shown by the browser's service worker (see
/// wwwroot/service-worker.js in Orbit.Web) once delivered. <paramref name="Url"/> is the in-app page
/// the notification should open when clicked - e.g. the calendar event, chat conversation, or task list
/// it's about.
/// </summary>
public sealed record PushNotificationPayload(string Title, string Body, string Url);
