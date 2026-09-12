using Orbit.Contracts;
using Orbit.Contracts.Calendar;

namespace Orbit.Contracts.Places;

/// <summary>
/// What a place is, as the form saves it - one request for making one and for changing one, because a
/// place is small enough that the two say exactly the same thing.
///
/// Nothing here is "not provided": the form holds every field and sends every field, so a missing one is
/// a client that meant to clear it. That is the opposite of the rule a task entry's newer fields follow,
/// and deliberately so - those exist because two clients disagree about what an entry carries, and
/// nothing but this form has ever written a place.
/// </summary>
/// <param name="IsPrivate">
/// Sealed unless the form says otherwise, which is the opposite default from everything else here - see
/// Orbit.Core.Places.Place.IsPrivate. A client that has not been taught about sealing therefore cannot
/// make an open place by leaving a field out.
/// </param>
/// <param name="EncryptedContent">
/// The sealed name, description and point - see SealedPlace. Required when <paramref name="IsPrivate"/>
/// is true, and the three readable fields above are then ignored: the server refuses a private place
/// that arrives with nothing sealed in it.
/// </param>
/// <param name="SourceTaskItemId">
/// The task entry this place is made from - see Orbit.Core.Places.Place.SourceTaskItemId. The one
/// exception to "nothing here is not provided": on a change, null leaves the entry a place answers to
/// alone, because the phone knows nothing of it and sends none, and editing a place there must not cut it
/// loose from its entry.
/// </param>
public sealed record SavePlaceRequest(
    string Name,
    EventLocationDto Where,
    string Description = "",
    string Colour = "",
    string Priority = "Normal",
    IReadOnlyList<Guid>? TaskListIds = null,
    bool IsPrivate = true,
    EncryptedContentDto? EncryptedContent = null,
    Guid? SourceTaskItemId = null);
