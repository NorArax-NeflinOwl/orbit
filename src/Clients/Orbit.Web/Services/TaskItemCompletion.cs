using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Orbit.Contracts.Tasks;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;

namespace Orbit.Web.Services;

/// <summary>What ticking one entry off came to.</summary>
public enum TaskItemTickOutcome
{
    /// <summary>Written. What the entry now says is what the server holds.</summary>
    Ticked,

    /// <summary>It was tried and refused. The screen says so; see <see cref="TaskItemCompletion.FailureMessage"/>.</summary>
    Failed,

    /// <summary>
    /// Nobody is signed in any more. The reader has been sent to the login page, so there is nothing
    /// for the screen to say and nothing left of it to say it on.
    /// </summary>
    SignedOut
}

/// <summary>
/// Crossing one entry of a task list off, asked the same way from every screen that offers it - the
/// checklist, where the whole list is in front of the reader, and the entry's own page, where one is.
/// Written once because the interesting parts are not the save: an entry standing for other lists is
/// not ticked by hand at all, and crossing off "Update stock levels" is a claim about a whole shelf.
///
/// The update endpoint replaces the list wholesale, so a tick sends every entry back with one of them
/// changed. The entry to change is found **by its id**, and by position only for one that has none -
/// an entry nobody has saved yet, since `TaskItemRequest.Id` has been kept through a save since
/// 2026-09-06.
///
/// Position was the rule until 2026-09-10, on the strength of a comment saying a save regenerates ids,
/// which had stopped being true. It mostly worked - `IndexOf` compares a record by every field, ids
/// included - and failed in the one case nobody would look at: a caller holding a copy of the entry
/// that is no longer equal to the stored one, because a second reader changed another of its fields.
/// `IndexOf` then answers -1, no entry matches, and the list is saved back **with nothing ticked**,
/// reporting success.
/// </summary>
public sealed class TaskItemCompletion(
    TasksApiClient tasksApiClient,
    NavigationManager navigationManager,
    IJSRuntime jsRuntime,
    Translations translations,
    ILogger<TaskItemCompletion> logger)
{
    /// <summary>Why the last tick did not save, for the screen to show. Null until one fails.</summary>
    public string? FailureMessage { get; private set; }

    /// <summary>
    /// What a tick did beyond the entry itself, for the screen to say - the shelf brought up to its
    /// minimums by finishing a round of restocking. Null for an ordinary tick, which speaks for itself.
    /// </summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Whether this entry is ticked here at all. An entry standing for other lists is done when they
    /// are (see LinkedTaskCompletionResolver), so its box is not a box the reader can fill in - the
    /// screens say which list the answer is on instead.
    /// </summary>
    public static bool IsTickedElsewhere(TaskItemDto item) => item.AllLinkedTaskListIds.Count > 0;

    /// <summary>
    /// Whether this list is the reader's to change. A read-only share can be ticked through neither
    /// screen: the server refuses it, and a box that answers a press with a refusal is worse than one
    /// that never offered.
    /// </summary>
    public static bool CanBeTicked(TaskDto taskList)
        => taskList.AccessLevel == nameof(ShareAccessLevel.CanEdit);

    /// <param name="state">
    /// What the box now says - see <see cref="TickState"/>. Three answers rather than two: an entry can
    /// be crossed out as well as ticked off, and both mean it is finished with.
    /// </param>
    public async Task<TaskItemTickOutcome> TickAsync(
        TaskDto taskList, TaskItemDto item, TickState state, CancellationToken cancellationToken = default)
    {
        FailureMessage = null;
        Note = null;
        // Only a tick claims the whole round is done. Crossing the reminder out says the opposite, and
        // topping every shelf up over it would be acting on the answer it was not given.
        if (state == TickState.Completed && await FinishedTheWholeRestockAsync(taskList, item, cancellationToken))
        {
            return FailureMessage is null ? TaskItemTickOutcome.Ticked : TaskItemTickOutcome.Failed;
        }

        // By id, and by position only for an entry that has none - see the class comment.
        var toggledIndex = item.Id == Guid.Empty ? taskList.Items.ToList().IndexOf(item) : -1;
        // Everything as it already is, with one entry's answer changed - see TaskItemRequest.From on
        // why the fields are not listed here.
        var items = taskList.Items
            .Select((existingItem, index) =>
            {
                var isTheOneBeingTicked = item.Id == Guid.Empty
                    ? index == toggledIndex
                    : existingItem.Id == item.Id;

                return TaskItemRequest.From(existingItem) with
                {
                    IsCompleted = isTheOneBeingTicked ? state.IsCompleted() : existingItem.IsCompleted,
                    IsFailed = isTheOneBeingTicked ? state.IsFailed() : existingItem.IsFailed
                };
            })
            .ToList();

        try
        {
            var outcome = await tasksApiClient.UpdateTaskListAsync(
                taskList.Id, new UpdateTaskRequest(taskList.Title, items, taskList.IsGroup), cancellationToken);
            if (outcome.Kind == EditOutcomeKind.Locked)
            {
                FailureMessage = translations.Format(
                    "{0} is currently editing \"{1}\" - try again in a moment.", outcome.LockedByUserName, taskList.Title);
            }
            else if (outcome.Kind == EditOutcomeKind.NotFound)
            {
                FailureMessage = translations.Format("\"{0}\" is no longer available to you.", taskList.Title);
            }
            else if (outcome.Kind is EditOutcomeKind.Refused or EditOutcomeKind.ReadOnly)
            {
                // The server's own words, which it sends for exactly this reason - see EditOutcome.
                // A refusal used to pass for a save here, so the box stayed ticked until the reload
                // put it back and nothing on screen said why.
                FailureMessage = outcome.Reason
                    ?? translations["This was shared with you to read, not to change."];
            }
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            navigationManager.NavigateTo("/login");
            return TaskItemTickOutcome.SignedOut;
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Failed to toggle an item on task list {TaskListId}", taskList.Id);
            FailureMessage = translations["Couldn't save that change. Try again."];
        }

        return FailureMessage is null ? TaskItemTickOutcome.Ticked : TaskItemTickOutcome.Failed;
    }

    /// <summary>
    /// Crossing off "Update stock levels" while errands are still open on the same list is either the
    /// end of a round of restocking or a tick on one reminder. Only the reader knows which, so they are
    /// asked - and answering yes finishes the rest and brings the shelf up to its minimums in one go.
    /// Answers whether it handled the tick, so the ordinary save is skipped when it did.
    /// </summary>
    private async Task<bool> FinishedTheWholeRestockAsync(
        TaskDto taskList, TaskItemDto item, CancellationToken cancellationToken)
    {
        if (item.Description != RestockTaskNaming.UpdateStockReminderDescription
            || !taskList.Items.Any(other => other.Id != item.Id && !other.IsCompleted))
        {
            return false;
        }

        if (!await jsRuntime.InvokeAsync<bool>(
                "confirm", cancellationToken,
                translations["Finish this list and set every item in the inventory to its minimum?"]))
        {
            return false;
        }

        try
        {
            var toppedUp = await tasksApiClient.FinishRestockingAsync(taskList.Id, cancellationToken);
            logger.LogInformation(
                "User finished restocking from task list {TaskListId}, topping up {ToppedUpCount} items", taskList.Id, toppedUp);
            Note = translations.Format("{0} brought up to their minimum.", toppedUp);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Failed to finish restocking from task list {TaskListId}", taskList.Id);
            FailureMessage = translations["Couldn't finish the restocking. Try again."];
        }

        return true;
    }
}
