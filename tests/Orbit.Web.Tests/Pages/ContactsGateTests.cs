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
/// Which combinations of permissions leave the Contacts page usable. The page is about other people
/// existing, so it needs Contacts and nothing else - conversations are a separate unlock reached from
/// here, and location has nothing to do with it.
/// </summary>
public sealed class ContactsGateTests : OrbitTestContext
{
    public ContactsGateTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Theory]
    [InlineData(new[] { nameof(ApplicationPermission.Contacts) }, false)]
    [InlineData(new[] { nameof(ApplicationPermission.Contacts), nameof(ApplicationPermission.Chat) }, false)]
    [InlineData(new[] { nameof(ApplicationPermission.Location) }, true)]
    [InlineData(new string[0], true)]
    public void The_page_is_locked_until_this_account_can_see_other_people(string[] granted, bool expectedLocked)
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
    [Fact]
    public void A_locked_page_offers_no_search_box()
    {
        // A search box that answers every query with a refusal is worse than no search box.
        RegisterPermissions([]);
        RegisterContacts();

        var cut = RenderComponent<Web.Pages.Contacts>();

        Assert.Empty(cut.FindAll("input"));
        Assert.DoesNotContain("Group chats", cut.Markup);
    }

    [Fact]
    public void An_unlocked_page_still_has_one()
    {
        RegisterPermissions([nameof(ApplicationPermission.Contacts)]);
        RegisterContacts();

        var cut = RenderComponent<Web.Pages.Contacts>();

        Assert.NotEmpty(cut.FindAll("input"));
    }

    [Fact]
    public void The_groups_this_account_is_in_are_listed_beside_its_chats()
    {
        RegisterPermissions([nameof(ApplicationPermission.Contacts), nameof(ApplicationPermission.Chat)]);
        RegisterContactsAndGroups(groupsJson: """[{"id":"11111111-1111-1111-1111-111111111111","name":"Weekend trip","createdByUserId":"22222222-2222-2222-2222-222222222222","createdAtUtc":"2026-01-01T00:00:00+00:00","ownRole":"Member","members":[]}]""");

        var cut = RenderComponent<Web.Pages.Contacts>();

        // A group is a conversation like any other; this was the one page in the app that left them out.
        Assert.Contains("Weekend trip", cut.Markup);
    }

    [Fact]
    public void An_account_without_chat_is_not_shown_groups_it_could_not_open()
    {
        RegisterPermissions([nameof(ApplicationPermission.Contacts)]);
        RegisterContactsAndGroups(groupsJson: """[{"id":"11111111-1111-1111-1111-111111111111","name":"Weekend trip","createdByUserId":"22222222-2222-2222-2222-222222222222","createdAtUtc":"2026-01-01T00:00:00+00:00","ownRole":"Member","members":[]}]""");

        var cut = RenderComponent<Web.Pages.Contacts>();

        Assert.DoesNotContain("Weekend trip", cut.Markup);
    }

    private void RegisterContactsAndGroups(string groupsJson)
    {
        var handler = new StubHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                request.RequestUri!.AbsolutePath.EndsWith("/groups", StringComparison.Ordinal) ? groupsJson : "[]",
                Encoding.UTF8,
                "application/json")
        });
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new ChatApiClient(httpClient));
        Services.AddSingleton(new UsersApiClient(httpClient));
    }
}
