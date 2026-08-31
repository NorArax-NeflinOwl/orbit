using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orbit.Core.Sync;

namespace Orbit.Mobile.Data;

/// <summary>What a copy taken offline is a copy of - see <see cref="ICopyReviewStore"/>.</summary>
public enum CopyKind
{
    Note,

    TaskList,

    CalendarEvent,

    Warehouse
}

/// <summary>
/// The five things every copy carries, whatever it is a copy of. Kept on the entity itself rather than
/// in a table of its own, because a copy has to be openable and writable by the ordinary screen for its
/// kind - that is the whole point of taking one - and a screen reads its own table.
/// </summary>
public interface ICopyableForEditing
{
    Guid LocalId { get; set; }

    /// <summary>What this was copied from, or null when it is not a copy at all.</summary>
    Guid? CopyOfLocalId { get; set; }

    DateTimeOffset? CopiedAtUtc { get; set; }

    /// <summary>What the original was called when the copy was taken.</summary>
    string CopyBaseTitle { get; set; }

    /// <summary>
    /// What the original said when the copy was taken, already rendered as the lines a review shows.
    /// Rendered rather than stored structurally so that one diff serves all four kinds: what a reader
    /// compares is words on a screen, and each repository knows how to write its own.
    /// </summary>
    IReadOnlyList<string> CopyBaseLines { get; set; }

    /// <summary>
    /// Kept on purpose by a review, rather than still waiting for one. A kept copy is a thing in its own
    /// right; an unkept one is a question, and the difference decides both what is asked and what is sent.
    /// </summary>
    bool IsKeptCopy { get; set; }
}

/// <summary>
/// One copy as the review and history windows read it - the same shape for all four kinds, so there is
/// one review screen rather than four.
/// </summary>
/// <param name="BaseLines">What the original said when the copy was taken; the point both sides diff from.</param>
/// <param name="Lines">What the copy says now.</param>
/// <param name="OriginalLines">
/// What the original says now, or null when it has since been deleted - in which case the copy is all
/// that is left of it and there is nothing to apply it over.
/// </param>
public sealed record CopyUnderReview(
    CopyKind Kind, Guid LocalId, Guid OriginalLocalId, string Title, DateTimeOffset CopiedAtUtc,
    IReadOnlyList<string> BaseLines, IReadOnlyList<string> Lines, IReadOnlyList<string>? OriginalLines);

/// <summary>
/// What a repository has to answer for the review window to work over its kind. Each of the four
/// implements it; the window itself knows none of them apart, which is what keeps one screen able to
/// decide between a note, a task list, an appointment and a warehouse.
/// </summary>
public interface ICopyReviewStore
{
    CopyKind Kind { get; }

    Task<IReadOnlyList<CopyUnderReview>> GetCopiesAwaitingReviewAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CopyUnderReview>> GetKeptCopiesAsync(CancellationToken cancellationToken = default);

    /// <summary>Puts the copy's words onto what it came from, and drops the copy. "Keep mine".</summary>
    Task<LocalWriteOutcome> ApplyCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default);

    /// <summary>Drops the copy and leaves the original as it stands. "Keep theirs".</summary>
    Task<LocalWriteOutcome> DiscardCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the copy a thing of its own, still pointing at what it came from. "Keep both".
    ///
    /// <b>This is where a copy sheds the inner ids it was carrying.</b> A copy keeps the ids of the
    /// entries or shelf items inside it, so that applying it back onto the original replaces words
    /// rather than identity - an entry linked to an appointment stays that entry. The moment it becomes
    /// a thing of its own those ids belong to something else, so they are re-issued here.
    /// </summary>
    Task<LocalWriteOutcome> KeepCopyAsync(Guid copyLocalId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The half of copying-for-editing that is the same whatever is being copied: which rows are waiting,
/// which were kept, and what taking one away means. Written once against
/// <see cref="ICopyableForEditing"/> rather than four times, because the four bodies were identical
/// apart from the table they read.
///
/// What is <i>not</i> here is creating a copy and applying one: both touch what the thing actually
/// says, which is the one part each kind has to answer for itself.
/// </summary>
public static class CopiesForEditing
{
    /// <summary>Copies of this kind that no review has answered yet, newest first.</summary>
    public static async Task<IReadOnlyList<TEntity>> AwaitingReviewAsync<TEntity>(
        OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
        where TEntity : class, ICopyableForEditing
        => await dbContext.Set<TEntity>().AsNoTracking()
            .Where(copy => copy.CopyOfLocalId != null && !copy.IsKeptCopy)
            .OrderByDescending(copy => copy.CopiedAtUtc)
            .ToListAsync(cancellationToken);

    /// <summary>Copies of this kind kept on purpose - what the History window lists.</summary>
    public static async Task<IReadOnlyList<TEntity>> KeptAsync<TEntity>(
        OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
        where TEntity : class, ICopyableForEditing
        => await dbContext.Set<TEntity>().AsNoTracking()
            .Where(copy => copy.IsKeptCopy)
            .OrderByDescending(copy => copy.CopiedAtUtc)
            .ToListAsync(cancellationToken);

    /// <summary>One copy, tracked, or null when it is not there or is not a copy.</summary>
    public static Task<TEntity?> FindCopyAsync<TEntity>(
        OrbitLocalDbContext dbContext, Guid copyLocalId, CancellationToken cancellationToken)
        where TEntity : class, ICopyableForEditing
        => dbContext.Set<TEntity>().FirstOrDefaultAsync(
            candidate => candidate.LocalId == copyLocalId && candidate.CopyOfLocalId != null, cancellationToken);

    /// <summary>
    /// Takes a copy out along with anything queued about it. An unreviewed copy queues nothing, but one
    /// kept and then applied would have a create waiting, and leaving that behind would push a thing the
    /// reader has just discarded.
    /// </summary>
    public static void Remove<TEntity>(OrbitLocalDbContext dbContext, TEntity copy, string entityType)
        where TEntity : class, ICopyableForEditing
    {
        var queued = dbContext.Outbox
            .Where(entry => entry.EntityType == entityType && entry.LocalId == copy.LocalId);

        dbContext.Outbox.RemoveRange(queued);
        dbContext.Set<TEntity>().Remove(copy);
        Settle(dbContext, copy.LocalId);
    }

    /// <summary>
    /// Marks a copy kept and queues it as the new thing it has become. The create is what sends it: it
    /// was held back until a review said what it was - see <see cref="ICopyReviewStore.KeepCopyAsync"/>.
    /// </summary>
    public static void Keep<TEntity>(
        OrbitLocalDbContext dbContext, TEntity copy, string entityType, DateTimeOffset now)
        where TEntity : class, ICopyableForEditing
    {
        copy.IsKeptCopy = true;
        Settle(dbContext, copy.LocalId);
        dbContext.Outbox.Add(new OutboxEntry
        {
            EntityType = entityType,
            LocalId = copy.LocalId,
            Operation = OutboxOperation.Create,
            QueuedAtUtc = now
        });
    }

    /// <summary>A copy taken offline that no review has answered yet.</summary>
    public static bool IsAwaitingReview(ICopyableForEditing entity)
        => entity is { CopyOfLocalId: not null, IsKeptCopy: false };

    /// <summary>
    /// Says in the notification feed that a copy is waiting to be decided on, naming what it is a copy
    /// of. Written in the same save as the copy itself, so the two cannot disagree.
    ///
    /// In the feed rather than only as a badge, because a badge says "something" and the reader needs
    /// to know <i>which</i> thing they wrote in - two rows called "Zakupy" are otherwise a puzzle.
    /// </summary>
    public static void Announce(
        OrbitLocalDbContext dbContext, CopyKind kind, Guid copyLocalId, string title, DateTimeOffset now)
        => dbContext.Notifications.Add(new LocalNotification
        {
            Id = Guid.NewGuid(),
            Kind = "CopyAwaitingReview",
            Title = "A copy is waiting to be reviewed",
            Body = WaitingDescription(kind),
            BodyArgumentsJson = JsonSerializer.Serialize(new[] { title }),
            Url = NoticeUrl(copyLocalId),
            CreatedAtUtc = now,
            IsRaisedHere = true
        });

    /// <summary>
    /// Takes that notice away, because the question it was asking has been answered. Called wherever a
    /// copy stops waiting - applied, discarded or kept - so the feed never advertises a decision that
    /// has already been made.
    /// </summary>
    public static void Settle(OrbitLocalDbContext dbContext, Guid copyLocalId)
    {
        var url = NoticeUrl(copyLocalId);
        dbContext.Notifications.RemoveRange(
            dbContext.Notifications.Where(notice => notice.IsRaisedHere && notice.Url == url));
    }

    /// <summary>Which copy a notice is about - see NotificationDestination's "copies" path.</summary>
    private static string NoticeUrl(Guid copyLocalId) => $"/copies/{copyLocalId}";

    /// <summary>
    /// One whole sentence per kind rather than a noun dropped into a shared one: Polish declines what
    /// was copied, so "kopii notatki" and "…listy zadań" cannot come from the same template.
    /// </summary>
    private static string WaitingDescription(CopyKind kind)
        => kind switch
        {
            CopyKind.Note => "You wrote in a copy of the note “{0}” while you were offline.",
            CopyKind.TaskList => "You wrote in a copy of the task list “{0}” while you were offline.",
            CopyKind.CalendarEvent => "You wrote in a copy of the appointment “{0}” while you were offline.",
            _ => "You wrote in a copy of the warehouse “{0}” while you were offline."
        };
}
