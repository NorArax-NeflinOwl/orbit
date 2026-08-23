namespace Orbit.Core.Abstractions;

/// <summary>
/// What a Share*CommandHandler (ShareNoteCommandHandler, ShareTaskListCommandHandler,
/// ShareCalendarEventCommandHandler) did with a share request, once the source resource was found and
/// the caller had permission to share it. <see cref="AlreadyShared"/> distinguishes "a new offer was
/// created" from "an offer to this recipient already existed, so nothing new was created" - the two
/// look the same from the caller's point of view (a valid <see cref="ShareId"/> to hand to the
/// recipient) but the client uses the flag to word its own message differently ("shared" vs. "already
/// shared - sent a reminder"), and to skip creating a brand-new chat notice, reusing the existing
/// share's id as a reminder link instead. A null ShareOutcome (the handler's actual return type is
/// ShareOutcome?) means the resource doesn't exist, isn't accessible to the caller, or the caller isn't
/// allowed to share it - the same "not found" response either way, since telling those apart would leak
/// whether a given id exists to someone who can't already see it.
/// </summary>
public sealed record ShareOutcome(Guid ShareId, bool AlreadyShared);
