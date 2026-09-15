using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Api.Permissions;
using Orbit.Contracts;
using Orbit.Contracts.Folders;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Sharing;
using Orbit.Core.Abstractions;
using Orbit.Api.Sync;
using Orbit.Core.Notes;
using Orbit.Core.Sync;
using Orbit.Core.Notes.AcceptNoteShare;
using Orbit.Core.Notes.AcquireNoteLock;
using Orbit.Core.Notes.CreateNote;
using Orbit.Core.Notes.DeleteNote;
using Orbit.Core.Notes.DuplicateNote;
using Orbit.Core.Notes.GetNoteById;
using Orbit.Core.Notes.GetNoteShareStatus;
using Orbit.Core.Notes.GetNotes;
using Orbit.Core.Notes.MoveNoteToFolder;
using Orbit.Core.Notes.ReleaseNoteLock;
using Orbit.Core.Notes.SetNotePinned;
using Orbit.Core.Notes.ShareNote;
using Orbit.Core.Notes.UpdateNote;
using Orbit.Core.Notes.ArchiveNote;

namespace Orbit.Api.Notes;

public static class NoteEndpoints
{
    public static void MapNoteEndpoints(this WebApplication app)
    {
        // Every note belongs to exactly one user (see GetUserId below), so the whole group requires a
        // valid, authenticated caller.
        var notes = app.MapGroup("/api/notes").RequireAuthorization();

        notes.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetNotesQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(result.Select(ToDto));
        });

        // What a client needs to catch up after being away, in one call: the notes that changed and
        // the ids that are gone. Kept separate from GET / rather than adding a parameter to it, so the
        // existing full-list response shape stays exactly as it was for the web client.
        notes.MapGet("/changes", async (
            DateTimeOffset since, ClaimsPrincipal user, IDispatcher dispatcher,
            ISyncTombstoneRepository tombstones, CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            var cursor = ChangeFeed.StartCursor();
            // The cursor goes to the database rather than being applied to everything it returned.
            var notes = await dispatcher.SendAsync(new GetNotesQuery(userId, since), cancellationToken);
            var changed = notes.Select(ToDto).ToList();

            return Results.Ok(await ChangeFeed.BuildAsync(
                changed, cursor, userId, SyncEntityType.Note, since, tombstones, cancellationToken));
        });

        notes.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var note = await dispatcher.SendAsync(new GetNoteByIdQuery(GetUserId(user), id), cancellationToken);
            return note is null ? Results.NotFound() : Results.Ok(ToDto(note));
        });

        notes.MapPost("/", async (
            CreateNoteRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var id = await dispatcher.SendAsync(
                new CreateNoteCommand(
                    GetUserId(user), request.Title, ToDomainContent(request.Content), request.IsPrivate,
                    ToDomainPayload(request.EncryptedContent), RequestEnum.Parse<ItemPriority>(request.Priority, "priority"),
                    request.FolderId, request.Tags),
                cancellationToken);
            return Results.Created($"/api/notes/{id}", id);
        });

        notes.MapPut("/{id:guid}", async (
            Guid id, UpdateNoteRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new UpdateNoteCommand(
                    GetUserId(user), id, request.Title, ToDomainContent(request.Content), request.IsPrivate,
                    ToDomainPayload(request.EncryptedContent), RequestEnum.Parse<ItemPriority>(request.Priority, "priority"),
                    request.Tags, request.PictureIds),
                cancellationToken);
            return ToApiResult(outcome);
        });

        // Separate from the update above because pinning only moves a card on a page: it needs no body
        // to send back, takes no edit lock, and works from the list page where nothing has been loaded
        // to edit - see Note.SetPinned.
        notes.MapPut("/{id:guid}/pinned", async (
            Guid id, SetNotePinnedRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var pinned = await dispatcher.SendAsync(
                new SetNotePinnedCommand(GetUserId(user), id, request.IsPinned), cancellationToken);
            return pinned ? Results.NoContent() : Results.NotFound();
        });

        // Filing, which is its own endpoint for the same reason pinning is - and for one more: an
        // update carries the whole note, so a folder sent with it would be emptied by every client that
        // has not heard of folders. See MoveNoteToFolderCommand.
        notes.MapPut("/{id:guid}/folder", async (
            Guid id, MoveToFolderRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var moved = await dispatcher.SendAsync(
                new MoveNoteToFolderCommand(GetUserId(user), id, request.FolderId), cancellationToken);
            return moved ? Results.NoContent() : Results.NotFound();
        });

        // Putting one away and bringing it back - see ArchiveNoteCommand. Its own endpoint beside
        // the filing above, and for the same reason: an update carries the whole note.
        notes.MapPut("/{id:guid}/archived", async (
            Guid id, ArchiveRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var archived = await dispatcher.SendAsync(
                new ArchiveNoteCommand(GetUserId(user), id, request.IsArchived), cancellationToken);
            return archived ? Results.NoContent() : Results.NotFound();
        });

        // A second note saying the same thing - see DuplicateNoteCommand. The body is optional, and a
        // caller that sends none keeps the original's title.
        notes.MapPost("/{id:guid}/duplicate", async (
            Guid id, DuplicateRequest? request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var copyId = await dispatcher.SendAsync(
                new DuplicateNoteCommand(GetUserId(user), id, request?.Name), cancellationToken);
            return copyId is { } newId ? Results.Created($"/api/notes/{newId}", newId) : Results.NotFound();
        });

        notes.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteNoteCommand(GetUserId(user), id), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        // Acquires (or refreshes) the edit lock on a note the caller has CanEdit access to - see
        // AcquireNoteLockCommand. NoteEditor.razor calls this once on opening an editable note, then
        // again on a heartbeat while the editor stays open.
        notes.MapPost("/{id:guid}/lock", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(new AcquireNoteLockCommand(GetUserId(user), id), cancellationToken);
            return ToApiResult(outcome);
        });

        // Releases the caller's own edit lock, if they hold one - a no-op otherwise. Always 204, since
        // there's nothing meaningful to distinguish from the caller's point of view (see
        // ReleaseNoteLockCommand).
        notes.MapDelete("/{id:guid}/lock", async (Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new ReleaseNoteLockCommand(GetUserId(user), id), cancellationToken);
            return Results.NoContent();
        });

        // Offers a copy of an owned note to another user - see ShareNoteCommand. The client is
        // responsible for notifying the recipient (a chat message carrying the returned share id),
        // since only the browser holds the key material to encrypt that message - mirrors
        // CalendarEndpoints' equivalent share endpoint.
        notes.MapPost("/{id:guid}/shares", async (
            Guid id, ShareNoteRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var outcome = await dispatcher.SendAsync(
                new ShareNoteCommand(GetUserId(user), id, request.RecipientUserId, RequestEnum.Parse<ShareAccessLevel>(request.AccessLevel, "accessLevel")),
                cancellationToken);
            return outcome is null ? Results.NotFound() : Results.Ok(new ShareResultDto(outcome.ShareId, outcome.AlreadyShared, outcome.AccessLevelRaised));
        }).RequireAuthorization(PermissionPolicies.Sharing);

        // Resolves a share offered to the caller into a copy in their own notes - see AcceptNoteShareCommand.
        notes.MapPost("/shares/{shareId:guid}/accept", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var accepted = await dispatcher.SendAsync(new AcceptNoteShareCommand(GetUserId(user), shareId), cancellationToken);
            return accepted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(PermissionPolicies.Sharing);

        // Lets Chat.razor show an accurate "Accept" vs. "already accepted" state for a note-share message
        // even after a page reload, instead of only remembering what was clicked this session.
        notes.MapGet("/shares/{shareId:guid}/status", async (
            Guid shareId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var isAccepted = await dispatcher.SendAsync(new GetNoteShareStatusQuery(GetUserId(user), shareId), cancellationToken);
            return isAccepted is null ? Results.NotFound() : Results.Ok(isAccepted);
        }).RequireAuthorization(PermissionPolicies.Sharing);
    }

    /// <summary>
    /// Reads the authenticated user's id out of the JWT's "sub" claim. Safe to assume it's present and
    /// valid: the group requires authorization, and Orbit.Api only ever issues tokens with this claim
    /// (see TokenService).
    /// </summary>
    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }

    private static IReadOnlyList<NoteContentLine> ToDomainContent(IReadOnlyList<NoteContentLineDto> content)
        => content.Select(line => line.Table is { } table
            // A line that carries a table is the table and nothing else - see NoteContentLine.OfTable,
            // which squares it up and leaves no room for words or a box beside it. A picture the same.
            ? NoteContentLine.OfTable(TableOf(table))
            : line.Picture is { } picture
            ? NoteContentLine.OfPicture(new NotePictureLine(picture.PictureId, picture.ContentType, picture.WidthPixels, picture.HeightPixels))
            // And a rule across the note the same - see NoteContentLine.OfSeparator. What is written on
            // it is taken as it arrived: it was written when the rule was made, and the server's clock
            // has nothing to say about it.
            : line.Separator is { } separator
            ? NoteContentLine.OfSeparator(separator.Stamp)
            : new NoteContentLine(
                line.Text, line.IsChecklistItem, line.IsChecked, line.IsFailed && !line.IsChecked,
                StyleOf(line.Style), MarksOf(line.AllMarks, line.Text))).ToList();

    private static NoteTable TableOf(NoteTableDto table)
        => new([.. table.Rows.Select(row => new NoteTableRow(
            [.. row.Cells.Select(cell => new NoteTableCell(cell.Text, MarksOf(cell.AllMarks, cell.Text)))]))]);

    /// <summary>
    /// A line's marks as they arrived, put in the one shape the rules work in - clipped to the words,
    /// with anything this build does not know dropped. See NoteTextMarks.Normalized.
    /// </summary>
    private static IReadOnlyList<NoteTextRun> MarksOf(IReadOnlyList<NoteTextRunDto> marks, string text)
        => NoteTextMarks.Normalized(
            marks.Select(run => new NoteTextRun(run.Start, run.Length, NoteTextMarks.Read(run.Mark))),
            text.Length);

    private static NoteContentLineDto ToDto(NoteContentLine line)
        => new(line.Text, line.IsChecklistItem, line.IsChecked, line.IsFailed, line.Style.ToString(),
            MarksSent(line.AllMarks), TableSent(line.Table), PictureSent(line.Picture),
            line.Separator is null ? null : new NoteSeparatorLineDto(line.Separator.Stamp));

    private static NotePictureLineDto? PictureSent(NotePictureLine? picture)
        => picture is null ? null : new NotePictureLineDto(picture.PictureId, picture.ContentType, picture.WidthPixels, picture.HeightPixels);

    private static NoteTableDto? TableSent(NoteTable? table)
        => table is null
            ? null
            : new NoteTableDto([.. table.Rows.Select(row => new NoteTableRowDto(
                [.. row.Cells.Select(cell => new NoteTableCellDto(cell.Text, MarksSent(cell.AllMarks)))]))]);

    /// <summary>
    /// A line's marks as they go out - null rather than an empty list for a line with none, which is
    /// nearly every line of nearly every note: the field is then simply absent from the JSON.
    /// </summary>
    private static IReadOnlyList<NoteTextRunDto>? MarksSent(IReadOnlyList<NoteTextRun> marks)
        => marks.Count == 0
            ? null
            : marks.Select(run => new NoteTextRunDto(run.Start, run.Length, run.Mark.ToString())).ToList();

    /// <summary>
    /// A style read off a request. A word this build does not know reads as Body rather than being
    /// refused: a line drawn plainly is still the reader's line, where refusing the save loses what they
    /// wrote. See NoteLineStyles.Read, which is that rule and which every client reads styles by.
    /// </summary>
    private static NoteLineStyle StyleOf(string? style) => NoteLineStyles.Read(style);


    /// <summary>Both halves travel together or not at all, so a request carrying only one is treated as carrying neither.</summary>
    private static EncryptedPayload? ToDomainPayload(EncryptedContentDto? encryptedContent)
        => encryptedContent is null ? null : new EncryptedPayload(encryptedContent.Ciphertext, encryptedContent.Nonce);

    private static EncryptedContentDto? ToDto(EncryptedPayload? encryptedContent)
        => encryptedContent is null ? null : new EncryptedContentDto(encryptedContent.Ciphertext, encryptedContent.Nonce);

    private static NoteDto ToDto(Note note)
        => new(
            note.Id, note.Title, note.Content.Select(ToDto).ToList(), note.IsPrivate, ToDto(note.EncryptedContent),
            note.CreatedAtUtc, note.UpdatedAtUtc,
            note.IsShared, note.SharedByUserName, note.AccessLevel.ToString(), note.IsShared ? note.UserId : null,
            note.IsSharedWithOthers, note.IsPinnedForCaller, note.Priority.ToString(),
            // Filing is the owner's own, so a recipient is told nothing about it: the id would name a
            // folder that does not exist on their pages, and a card filed under a tab they cannot see
            // is a card that has vanished.
            note.IsShared ? null : note.FolderId,
            note.Tags,
            // Putting away is the owner's too, and for a stronger reason than the filing: a recipient who was
            // told this was archived would find it gone from their own pages over a decision that was never
            // theirs. False for them, which is where their own copy already is.
            !note.IsShared && note.IsArchived);

    /// <summary>Maps an EditOutcome onto the corresponding HTTP response - shared by the update and lock-acquire endpoints above.</summary>
    private static IResult ToApiResult(EditOutcome outcome) => outcome.Kind switch
    {
        EditOutcomeKind.Success => Results.NoContent(),
        EditOutcomeKind.Locked => Results.Json(new LockConflictDto(outcome.LockedByUserName!), statusCode: StatusCodes.Status409Conflict),
        // 403 rather than 404: the caller can see this, so hiding it from them now would only confuse.
        EditOutcomeKind.ReadOnly => Results.Json(
            new RefusalDto("This was shared with you to read, not to change."), statusCode: StatusCodes.Status403Forbidden),
        _ => Results.NotFound()
    };
}
