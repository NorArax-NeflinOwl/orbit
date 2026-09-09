using System.Net;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Filing a note or a task list under a folder travels on a request of its own, separately from the
/// thing being saved (see MoveToFolderRequest). Both editors used to throw the answer away and stamp
/// their "already filed there" marker regardless, so a refusal was invisible twice over: the thing
/// stayed where it was and said nothing, and the *next* Save skipped the move entirely because the page
/// now believed it was done. "Save does not save the folder" is what that looks like from outside.
///
/// These pin the half the pages now depend on - that a refusal is reported as one rather than as
/// success. Which refusals the server issues is its own business
/// (MoveTaskListToFolderCommandHandler: not the caller's list, or a folder that is not theirs).
/// </summary>
public sealed class FilingIntoAFolderTests
{
    [Fact]
    public async Task A_task_list_that_could_not_be_filed_says_so()
    {
        var client = new TasksApiClient(Refusing(HttpStatusCode.BadRequest));

        Assert.False(await client.MoveToFolderAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task A_note_that_could_not_be_filed_says_so()
    {
        var client = new NotesApiClient(Refusing(HttpStatusCode.BadRequest));

        Assert.False(await client.MoveToFolderAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    /// <summary>
    /// The guard on both, so neither can pass by never reaching the server at all: the same call
    /// against a server that accepts it answers true.
    /// </summary>
    [Fact]
    public async Task Filing_that_worked_is_reported_as_having_worked()
    {
        Assert.True(await new TasksApiClient(Accepting()).MoveToFolderAsync(Guid.NewGuid(), Guid.NewGuid()));
        Assert.True(await new NotesApiClient(Accepting()).MoveToFolderAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    /// <summary>
    /// And "no folder at all" is a filing like any other - it is how something is taken out of a folder
    /// and left in the built-in one it belongs to. It would be easy to write a guard that treats a null
    /// id as nothing to do.
    /// </summary>
    [Fact]
    public async Task Taking_something_out_of_every_folder_is_still_sent()
    {
        var sent = new List<string>();
        var client = new TasksApiClient(Recording(sent));

        Assert.True(await client.MoveToFolderAsync(Guid.NewGuid(), folderId: null));
        Assert.Single(sent, path => path.EndsWith("/folder", StringComparison.Ordinal));
    }

    private static HttpClient Refusing(HttpStatusCode status)
        => new(new StubHttpMessageHandler(_ => new HttpResponseMessage(status)))
        {
            BaseAddress = new Uri("https://example.test/")
        };

    private static HttpClient Accepting()
        => new(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent)))
        {
            BaseAddress = new Uri("https://example.test/")
        };

    private static HttpClient Recording(List<string> paths)
        => new(new StubHttpMessageHandler(request =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
}
