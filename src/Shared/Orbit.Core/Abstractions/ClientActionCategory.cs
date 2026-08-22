namespace Orbit.Core.Abstractions;

/// <summary>
/// Groups the requests that represent an action a client explicitly performed (as opposed to internal
/// or background requests), so <see cref="LoggingDispatcher"/> can tag its log lines with the category
/// a person can filter and scan for.
/// </summary>
public enum ClientActionCategory
{
    Save,
    Login,
    Registration,
    Edit,
    SendMessage,

    /// <summary>Sharing any element with another user - currently only a calendar event; task list,
    /// note, and location sharing are planned but have no request type yet.</summary>
    ShareElement,

    /// <summary>Covers both subscribing to and unsubscribing from push notifications.</summary>
    PushNotificationToggle,
}
