namespace Orbit.Contracts.Calendar;

/// <summary>AccessLevel is "ReadOnly" or "CanEdit" (see Orbit.Core.Abstractions.ShareAccessLevel).</summary>
public sealed record ShareCalendarEventRequest(Guid RecipientUserId, string AccessLevel = "ReadOnly");
