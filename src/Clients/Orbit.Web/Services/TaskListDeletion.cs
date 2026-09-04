using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Orbit.Contracts.Tasks;

namespace Orbit.Web.Services;

/// <summary>What asking to delete a task list came to.</summary>
public enum TaskListDeletionOutcome
{
    /// <summary>The reader said no at one of the questions. Nothing was sent.</summary>
    Cancelled,
    Deleted,

    /// <summary>It was tried and refused. The screen says so; see <see cref="TaskListDeletion.FailureMessage"/>.</summary>
    Failed
}

/// <summary>
/// Deleting a task list, asked the same way from every screen that offers it - its card on /tasks, its
/// checklist, and its editor. Written once because the questions are the interesting part and three
/// copies of them would drift: a list can be deleted from three places now, and a group list asks a
/// second question that the other two kinds never do.
///
/// The confirmations are the browser's own <c>confirm</c>, which is what every other delete in Orbit
/// uses. Two of them rather than a dialog with three buttons: the second only appears for a group list
/// with something under it, and for every other list the flow is exactly the one that was there before.
/// </summary>
public sealed class TaskListDeletion(
    TasksApiClient tasksApiClient,
    NavigationManager navigationManager,
    IJSRuntime jsRuntime,
    Translations translations,
    ILogger<TaskListDeletion> logger)
{
    /// <summary>Why the last attempt failed, for the screen to show. Null until one does.</summary>
    public string? FailureMessage { get; private set; }

    /// <summary>The list as it was loaded - what the page of cards and the checklist both have in hand.</summary>
    public Task<TaskListDeletionOutcome> AskAndDeleteAsync(
        TaskDto taskList, CancellationToken cancellationToken = default)
        => AskAndDeleteAsync(taskList.Id, taskList.Title, GatheredBy(taskList), cancellationToken);

    /// <param name="gathers">
    /// How many other lists this one's entries stand for. Passed in rather than read off a DTO because
    /// the editor asks from a form that may have changed since it loaded: somebody who has just taken
    /// the links out should not be asked about lists this list no longer gathers.
    /// </param>
    public async Task<TaskListDeletionOutcome> AskAndDeleteAsync(
        Guid taskListId, string title, int gathers, CancellationToken cancellationToken = default)
    {
        FailureMessage = null;
        var named = title.Length > 0 ? title : translations["Untitled"];
        if (!await AskAsync(translations.Format("Delete task list \"{0}\"?", named)))
        {
            return TaskListDeletionOutcome.Cancelled;
        }

        // Only where there is something to ask about. A group list with no links under it - one being
        // built, or one whose links have all been removed - is an ordinary list as far as this goes.
        var deleteTheListsItGathers = false;
        if (gathers > 0)
        {
            // Said as a question about the other lists rather than about this one, and answerable
            // either way: "no" still deletes the group list, which is what the first question already
            // agreed to. What it gathers is a way of reading several lists together, and getting rid
            // of the reading is not the same as getting rid of what was being read.
            deleteTheListsItGathers = await AskAsync(gathers == 1
                ? translations["It gathers one other list. Delete that one too, along with anything it gathers in turn? Cancel keeps it."]
                : translations.Format(
                    "It gathers {0} other lists. Delete those too, along with anything they gather in turn? Cancel keeps them.",
                    gathers));
        }

        try
        {
            await tasksApiClient.DeleteTaskListAsync(taskListId, deleteTheListsItGathers, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Unauthorized)
        {
            navigationManager.NavigateTo("/login");
            return TaskListDeletionOutcome.Failed;
        }
        catch (HttpRequestException exception)
        {
            // Said on screen as well as logged: a delete that silently does nothing reads as a press
            // that never registered.
            logger.LogError(exception, "Failed to delete task list {TaskListId}", taskListId);
            FailureMessage = translations["Couldn't delete that task list. Check your connection and try again."];
            return TaskListDeletionOutcome.Failed;
        }

        logger.LogInformation(
            "Task list {TaskListId} deleted (gathered lists too: {DeletedTheListsItGathers})",
            taskListId, deleteTheListsItGathers);
        return TaskListDeletionOutcome.Deleted;
    }

    /// <summary>
    /// How many other lists this one's entries stand for. The lists those gather in turn are not counted
    /// here - the client holds only what it loaded, and the server is the one that reads the whole tree -
    /// so the question names the number it is sure of and says the rest goes with them.
    /// </summary>
    public static int GatheredBy(TaskDto taskList)
        => taskList.Items
            .SelectMany(item => item.AllLinkedTaskListIds)
            .Distinct()
            .Count();

    private async Task<bool> AskAsync(string question)
        => await jsRuntime.InvokeAsync<bool>("confirm", question);
}
