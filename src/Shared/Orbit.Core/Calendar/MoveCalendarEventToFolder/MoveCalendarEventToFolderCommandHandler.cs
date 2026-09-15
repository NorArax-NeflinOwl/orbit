using Orbit.Core.Abstractions;
using Orbit.Core.Folders;

namespace Orbit.Core.Calendar.MoveCalendarEventToFolder;

/// <summary>
/// Only the event's owner files it, and only into a folder of their own - the two checks
/// MoveNoteToFolderCommandHandler makes, and for the same reasons: a folder id arrives from a client,
/// and one belonging to somebody else would file this event under a tab its owner cannot see, which
/// reads as the event having vanished from the calendar.
/// </summary>
public sealed class MoveCalendarEventToFolderCommandHandler : IRequestHandler<MoveCalendarEventToFolderCommand, bool>
{
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly IFolderRepository _folderRepository;

    public MoveCalendarEventToFolderCommandHandler(
        ICalendarEventRepository calendarEventRepository, IFolderRepository folderRepository)
    {
        _calendarEventRepository = calendarEventRepository;
        _folderRepository = folderRepository;
    }

    public async Task<bool> HandleAsync(MoveCalendarEventToFolderCommand request, CancellationToken cancellationToken)
    {
        var calendarEvent = await _calendarEventRepository.GetByIdAsync(
            request.UserId, request.CalendarEventId, cancellationToken);
        if (calendarEvent is null || calendarEvent.UserId != request.UserId)
        {
            return false;
        }

        if (request.FolderId is { } folderId
            && await _folderRepository.GetByIdAsync(request.UserId, folderId, cancellationToken) is null)
        {
            return false;
        }

        calendarEvent.MoveToFolder(request.FolderId);
        await _calendarEventRepository.UpdateAsync(calendarEvent, cancellationToken);
        return true;
    }
}
