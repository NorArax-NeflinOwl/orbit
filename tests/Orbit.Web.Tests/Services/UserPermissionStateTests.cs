using System.Net;
using System.Text;
using Orbit.Core.Permissions;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

public sealed class UserPermissionStateTests
{
    /// <summary>Answers the permissions call only once <see cref="Release"/> is called, so a read can be caught mid-flight.</summary>
    private sealed class HeldPermissionsHandler(string permissionName) : HttpMessageHandler
    {
        private readonly TaskCompletionSource _released = new();

        public void Release() => _released.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await _released.Task;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"granted":["{{permissionName}}"]}""", Encoding.UTF8, "application/json")
            };
        }
    }

    [Fact]
    public async Task Waiting_for_the_first_read_waits_for_the_answer_and_not_just_the_question()
    {
        // The bug this pins: RefreshAsync used to mark the first read finished the moment it started, so
        // a page awaiting EnsureLoadedAsync carried on against an empty answer and drew "not unlocked
        // yet" for an account that had unlocked it - and, deciding its gating once, left it there.
        var handler = new HeldPermissionsHandler(nameof(ApplicationPermission.Chat));
        var state = new UserPermissionState(
            new UsersApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));

        var refresh = state.RefreshAsync();
        var waiting = state.EnsureLoadedAsync();
        Assert.False(waiting.IsCompleted);

        handler.Release();
        await Task.WhenAll(refresh, waiting);

        Assert.True(state.Has(ApplicationPermission.Chat));
    }

    [Fact]
    public async Task The_first_read_happens_once_however_many_pages_ask()
    {
        var requests = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            requests++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"granted":["Chat"]}""", Encoding.UTF8, "application/json")
            };
        });
        var state = new UserPermissionState(
            new UsersApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));

        await Task.WhenAll(state.EnsureLoadedAsync(), state.EnsureLoadedAsync(), state.EnsureLoadedAsync());

        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task A_failed_read_leaves_what_was_already_known_alone()
    {
        var shouldFail = false;
        var handler = new StubHttpMessageHandler(_ => shouldFail
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"granted":["Chat"]}""", Encoding.UTF8, "application/json")
            });
        var state = new UserPermissionState(
            new UsersApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
        await state.EnsureLoadedAsync();

        shouldFail = true;
        await state.RefreshAsync();

        // A dropped request is not evidence that somebody lost a permission.
        Assert.True(state.Has(ApplicationPermission.Chat));
    }
}
