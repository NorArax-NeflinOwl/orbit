using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;

namespace Orbit.Core.Places.CreatePlace;

/// <summary>Mirrors CreateNoteCommand - one command, the whole of what a place is, and its new id back.</summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record CreatePlaceCommand(
    Guid UserId, string Name, string Description, EventLocation Where, string Colour = "",
    ItemPriority Priority = ItemPriority.Normal,
    /// <summary>The task lists this place belongs to - see Place.TaskListIds.</summary>
    IReadOnlyList<Guid>? TaskListIds = null,
    /// <summary>Sealed unless the caller says otherwise - see Place.IsPrivate.</summary>
    bool IsPrivate = true,
    EncryptedPayload? EncryptedContent = null,
    /// <summary>The task entry this place is made from - see Place.SourceTaskItemId.</summary>
    Guid? SourceTaskItemId = null) : IRequest<Guid>;
