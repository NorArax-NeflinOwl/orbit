namespace Orbit.Mobile.Api;

/// <summary>
/// What the server did with a write the phone had already applied locally. Shared by every entity type
/// on the sync spine, because the three answers that matter are the same for all of them.
/// </summary>
public enum WriteOutcome
{
    Applied,

    /// <summary>
    /// The server took the request and would not have it: somebody else held the edit lock, or it is
    /// not this reader's to change at all. Under the offline policy both should be rare - shared items
    /// are not editable offline - but sharing can change while the phone is away, so it has to be
    /// handled rather than assumed impossible.
    ///
    /// Answered rather than thrown, because a queued change the server will never accept has to be
    /// given up on: an unhandled 403 escaped the outbox's own retry rules, so the change stayed queued,
    /// was sent again on every sync, and blocked every later change of its kind behind it.
    /// </summary>
    Refused,

    /// <summary>It is gone server-side. Nothing queued against it can ever succeed.</summary>
    Gone,

    /// <summary>
    /// The server says this cannot be done at all - a rule about the thing itself rather than about who
    /// holds it, answered as 400 with a message (see "Refusing a request"). Trying again changes
    /// nothing, which is what separates it from <see cref="Refused"/>.
    ///
    /// Mapped rather than thrown because a refusal by design is the API working: an unhandled
    /// HttpRequestException out of a screen's command takes the whole app down, which is what moving an
    /// entry onto a list it already stands for did.
    /// </summary>
    Rejected
}
