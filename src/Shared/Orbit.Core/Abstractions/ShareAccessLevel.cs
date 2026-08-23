namespace Orbit.Core.Abstractions;

/// <summary>
/// The level of access a share offer grants its recipient once accepted - shared by calendar event,
/// note, and task list sharing (see CalendarEventShare, NoteShare, TaskListShare) so all three use the
/// same concept instead of three copies of it. Declaration order is significant: the underlying int
/// value doubles as a rank (<see cref="ReadOnly"/> &lt; <see cref="Share"/> &lt; <see cref="CanEdit"/>),
/// which is how the Share*CommandHandler classes decide both whether a caller may re-share their copy at
/// all, and the highest level they're allowed to grant when they do - see ShareNoteCommandHandler's
/// class comment for the exact rule.
/// </summary>
public enum ShareAccessLevel
{
    /// <summary>The recipient can view their accepted copy but not change it or share it further - the default.</summary>
    ReadOnly,

    /// <summary>
    /// The recipient can't change their accepted copy, but can re-share it with others - at ReadOnly or
    /// Share, never CanEdit, since a re-share can never grant more than the re-sharer themselves holds.
    /// </summary>
    Share,

    /// <summary>The recipient can edit their accepted copy, and re-share it at any level.</summary>
    CanEdit
}
