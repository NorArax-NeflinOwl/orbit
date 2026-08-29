using System.Net;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Core.Permissions;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// The card for one person: who they are, and what this reader's conversation with them says. Reached
/// from the contact list, from the dashboard's contacts card, and from the chat thread's own menu.
/// </summary>
public sealed class ContactInfoTests : OrbitTestContext
{
    private static readonly Guid ContactUserId = Guid.NewGuid();

    public ContactInfoTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Fact]
    public void The_card_says_who_this_is()
    {
        RegisterClients(
            userJson: $$"""{"id":"{{ContactUserId}}","userName":"anna","displayName":"Anna Kowalska","publicKeyBase64":"key"}""",
            contactsJson: ContactListJson("Available"));

        var cut = Render();

        Assert.Contains("Anna Kowalska", cut.Find("h1").TextContent);
        Assert.Contains("anna", cut.Find(".contact-info-login").TextContent);
        Assert.Contains("anna@example.com", cut.Markup);
    }

    [Fact]
    public void It_says_whether_they_are_here_right_now()
    {
        RegisterClients(
            userJson: $$"""{"id":"{{ContactUserId}}","userName":"anna","displayName":"Anna Kowalska","publicKeyBase64":"key"}""",
            contactsJson: ContactListJson("Away"));

        var cut = Render();

        Assert.Contains("Away", cut.Markup);
    }

    [Fact]
    public void Somebody_never_spoken_to_has_no_conversation_to_describe()
    {
        // Found by search rather than already on the list: there is nothing to say about a conversation
        // that does not exist, and saying nothing at all would read as a page that failed to load.
        RegisterClients(
            userJson: $$"""{"id":"{{ContactUserId}}","userName":"anna","displayName":"Anna Kowalska","publicKeyBase64":"key"}""",
            contactsJson: "[]");

        var cut = Render();

        Assert.Contains("No conversation with them yet.", cut.Markup);
    }

    [Fact]
    public void An_account_Orbit_cannot_reach_says_so()
    {
        RegisterClients(userJson: null, contactsJson: "[]");

        var cut = Render();

        Assert.Contains("can't reach that account", cut.Markup);
    }

    [Fact]
    public void The_card_opens_the_conversation_with_them()
    {
        RegisterClients(
            userJson: $$"""{"id":"{{ContactUserId}}","userName":"anna","displayName":"Anna Kowalska","publicKeyBase64":"key"}""",
            contactsJson: ContactListJson("Available"));
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = Render();

        cut.FindAll(".page-header-actions button").First(button => button.TextContent.Contains("Open chat")).Click();

        Assert.EndsWith($"/chat/{ContactUserId}", navigationManager.Uri);
    }

    private IRenderedComponent<ContactInfo> Render()
        => RenderComponent<ContactInfo>(parameters => parameters.Add(page => page.UserId, ContactUserId));

    private static string ContactListJson(string presenceStatus)
        => $$"""
        [{"userId":"{{ContactUserId}}","userName":"anna","displayName":"Anna Kowalska","email":"anna@example.com",
          "publicKeyBase64":"key","lastMessageAtUtc":"2026-08-01T10:00:00+00:00",
          "requiresApprovalFromCurrentUser":false,"isPendingApprovalFromOtherParty":false,"unreadCount":0,
          "presenceStatus":"{{presenceStatus}}"}]
        """;

    /// <param name="userJson">Null stands for an account the API has nothing for - a 404 from /api/users.</param>
    private void RegisterClients(string? userJson, string contactsJson)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/api/users/", StringComparison.Ordinal))
            {
                return userJson is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : Json(userJson);
            }

            return Json(contactsJson);
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new ChatApiClient(httpClient));
        Services.AddSingleton(new UsersApiClient(httpClient));
        RegisterPermissions();
    }

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private void RegisterPermissions()
    {
        var granted = $"\"{nameof(ApplicationPermission.Contacts)}\",\"{nameof(ApplicationPermission.Chat)}\"";
        var permissions = new UserPermissionState(new UsersApiClient(
            new HttpClient(new StubHttpMessageHandler(_ => Json($"{{\"granted\":[{granted}]}}")))
            {
                BaseAddress = new Uri("https://example.test/")
            }));
        permissions.EnsureLoadedAsync().GetAwaiter().GetResult();
        Services.AddSingleton(permissions);
    }
}
