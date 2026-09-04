using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Tasks;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// The questions asked before a task list is deleted, which all three screens that offer it now ask
/// through. The second one exists only for a group list, and answering it either way still deletes the
/// list that was asked about - the first question already agreed to that.
/// </summary>
public sealed class TaskListDeletionTests : OrbitTestContext
{
    [Fact]
    public async Task An_ordinary_list_is_asked_about_once_and_takes_nothing_with_it()
    {
        var deletion = Build(out var sent, deleteIt: true, andTheGatheredOnes: false);

        var outcome = await deletion.AskAndDeleteAsync(List("Shopping"));

        Assert.Equal(TaskListDeletionOutcome.Deleted, outcome);
        Assert.Equal(["api/tasks/" + TaskListId], sent);
    }

    [Fact]
    public async Task Saying_no_to_the_first_question_sends_nothing_at_all()
    {
        var deletion = Build(out var sent, deleteIt: false, andTheGatheredOnes: false);

        var outcome = await deletion.AskAndDeleteAsync(List("Shopping"));

        Assert.Equal(TaskListDeletionOutcome.Cancelled, outcome);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task A_group_list_is_asked_a_second_time_and_yes_takes_the_gathered_lists_with_it()
    {
        var deletion = Build(out var sent, deleteIt: true, andTheGatheredOnes: true);

        var outcome = await deletion.AskAndDeleteAsync(GroupGathering("Everything", gathers: 2));

        Assert.Equal(TaskListDeletionOutcome.Deleted, outcome);
        Assert.Equal([$"api/tasks/{TaskListId}?deleteTheListsItGathers=true"], sent);
    }

    [Fact]
    public async Task Saying_no_to_the_second_question_still_deletes_the_group_list_itself()
    {
        // What it gathers is a way of reading several lists together. Getting rid of the reading is not
        // the same as getting rid of what was being read - and the first question already said yes.
        var deletion = Build(out var sent, deleteIt: true, andTheGatheredOnes: false);

        var outcome = await deletion.AskAndDeleteAsync(GroupGathering("Everything", gathers: 2));

        Assert.Equal(TaskListDeletionOutcome.Deleted, outcome);
        Assert.Equal(["api/tasks/" + TaskListId], sent);
    }

    [Fact]
    public async Task A_group_list_gathering_nothing_is_not_asked_the_second_question()
    {
        // One being built, or one whose links have all been taken out. There is nothing to ask about.
        // Answering yes to a question that is never asked would still not add the flag, so this holds
        // that the question is skipped rather than merely answered no.
        var deletion = Build(out var sent, deleteIt: true, andTheGatheredOnes: true);

        var outcome = await deletion.AskAndDeleteAsync(GroupGathering("Everything", gathers: 0));

        Assert.Equal(TaskListDeletionOutcome.Deleted, outcome);
        Assert.Equal(["api/tasks/" + TaskListId], sent);
    }

    [Fact]
    public async Task A_refusal_is_said_on_screen_rather_than_only_logged()
    {
        var deletion = Build(
            out _, deleteIt: true, andTheGatheredOnes: false, answerWith: HttpStatusCode.InternalServerError);

        var outcome = await deletion.AskAndDeleteAsync(List("Shopping"));

        Assert.Equal(TaskListDeletionOutcome.Failed, outcome);
        Assert.NotNull(deletion.FailureMessage);
    }

    private static readonly Guid TaskListId = Guid.NewGuid();

    /// <summary>
    /// The two confirmations are told apart by what they ask rather than by the order they come in, so a
    /// test that never reaches the second one still fails if the first is worded as the second.
    /// </summary>
    private TaskListDeletion Build(
        out List<string> sent, bool deleteIt, bool andTheGatheredOnes,
        HttpStatusCode answerWith = HttpStatusCode.NoContent)
    {
        var addresses = new List<string>();
        sent = addresses;
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            addresses.Add(request.RequestUri!.PathAndQuery.TrimStart('/'));
            return new HttpResponseMessage(answerWith);
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };

        JSInterop.Setup<bool>("confirm", invocation => Asked(invocation).StartsWith("Delete task list"))
            .SetResult(deleteIt);
        JSInterop.Setup<bool>("confirm", invocation => Asked(invocation).Contains("gathers"))
            .SetResult(andTheGatheredOnes);

        return new TaskListDeletion(
            new TasksApiClient(httpClient),
            Services.GetRequiredService<NavigationManager>(),
            JSInterop.JSRuntime,
            Services.GetRequiredService<Translations>(),
            NullLogger<TaskListDeletion>.Instance);
    }

    private static string Asked(JSRuntimeInvocation invocation) => invocation.Arguments[0]?.ToString() ?? string.Empty;

    private static TaskDto List(string title) => new(
        TaskListId, title, [], IsCompleted: false, IsGroup: false, IsPrivate: false, EncryptedContent: null,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
        IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    private static TaskDto GroupGathering(string title, int gathers) => List(title) with
    {
        IsGroup = true,
        Items = [Item([.. Enumerable.Range(0, gathers).Select(_ => Guid.NewGuid())])]
    };

    private static TaskItemDto Item(IReadOnlyList<Guid> linkedTaskListIds) => new(
        Guid.NewGuid(), "Follows other lists", DueDateUtc: null, IsCompleted: false, LinkedTaskListId: null,
        OverdueNotificationChannel: "None", RemindDaily: false, DailyReminderNotificationChannel: "None",
        DailyReminderTimeOfDay: new TimeOnly(9, 0), LinkedTaskListIds: linkedTaskListIds);
}
