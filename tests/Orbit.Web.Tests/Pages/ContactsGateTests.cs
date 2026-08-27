using System.Net;
using System.Text;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Core.Permissions;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// Which combinations of permissions leave the Contacts page usable. Either kind of conversation is
/// reached from there, so one of the two is enough - and the condition that says so is easy to write
/// wrong: without brackets, "not chat or group chat" locks the page for precisely the accounts that
/// have group chat.
/// </summary>
public sealed class ContactsGateTests : OrbitTestContext
{
    public ContactsGateTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Theory]
    [InlineData(new[] { nameof(ApplicationPermission.Chat) }, false)]
    [InlineData(new[] { nameof(ApplicationPermission.GroupChat) }, false)]
    [InlineData(new[] { nameof(ApplicationPermission.Chat), nameof(ApplicationPermission.GroupChat) }, false)]
    [InlineData(new[] { nameof(ApplicationPermission.Location) }, true)]
    [InlineData(new string[0], true)]
    public void The_page_is_locked_only_when_neither_kind_of_conversation_is_unlocked(string[] granted, bool expectedLocked)
    {
        RegisterPermissions(granted);
        RegisterContacts();

        var cut = RenderComponent<Web.Pages.Contacts>();

        Assert.Equal(expectedLocked, cut.FindAll(".feature-locked").Count == 1);
    }

    private void RegisterPermissions(string[] granted)
    {
        var names = string.Join(",", granted.Select(name => $"\"{name}\""));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($"{{\"granted\":[{names}]}}", Encoding.UTF8, "application/json")
        });
        var permissions = new UserPermissionState(
            new UsersApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
        permissions.EnsureLoadedAsync().GetAwaiter().GetResult();
        Services.AddSingleton(permissions);
    }

    /// <summary>Nobody to show either way - what these tests are about is whether the page offers to look.</summary>
    private void RegisterContacts()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new ChatApiClient(httpClient));
        Services.AddSingleton(new UsersApiClient(httpClient));
    }
}
