using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;

namespace Orbit.Core.Places.UpdatePlace;

/// <summary>
/// Everything about a place, as the form saves it. False back when there is nothing of this user's under
/// that id - a place somebody else owns and one that never existed answer the same way, because telling
/// them apart would say whether an id exists.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record UpdatePlaceCommand(
    Guid UserId, Guid Id, string Name, string Description, EventLocation Where, string Colour = "",
    ItemPriority Priority = ItemPriority.Normal,
    IReadOnlyList<Guid>? TaskListIds = null,
    /// <inheritdoc cref="CreatePlace.CreatePlaceCommand.IsPrivate"/>
    bool IsPrivate = true,
    EncryptedPayload? EncryptedContent = null) : IRequest<bool>;
