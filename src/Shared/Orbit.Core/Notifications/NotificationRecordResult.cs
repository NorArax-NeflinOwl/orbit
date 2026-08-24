namespace Orbit.Core.Notifications;

/// <summary>
/// AllowedChannel is requestedChannel with any globally-disabled channel stripped out - callers should
/// send push/email using this, not the raw per-item NotificationChannel. EntryRecorded is true whenever
/// a NotificationEntry was actually added to the feed (i.e. the user's master AllowNotifications switch
/// is on); background services treat this the same as a successful push/email attempt for their
/// claim-then-send idempotency guard, since a recorded feed entry is itself a real "the user was
/// notified" event that must not repeat on the next poll just because both delivery channels happened
/// to be globally off.
/// </summary>
public sealed record NotificationRecordResult(NotificationChannel AllowedChannel, bool EntryRecorded);
