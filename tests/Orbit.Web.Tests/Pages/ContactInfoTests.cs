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
    public void An_id_that_resolves_to_nothing_says_why_it_cannot_be_more_specific()
    {
        // An account that has not unlocked Contacts is unfindable on purpose, and Orbit answers a
        // lookup for it exactly as it answers a lookup for nobody. This page cannot tell the two apart,
        // so it says that rather than picking one - and rather than the old "can't reach that account
        // right now", which read as a fault Orbit was having.
        RegisterClients(userJson: null, contactsJson: "[]");

        var cut = Render();

        Assert.Contains("nothing to show for this account", cut.Markup);
        Assert.Contains("answers both the same way", cut.Markup);
    }

    [Fact]
    public void Somebody_you_talk_to_who_has_gone_unfindable_is_told_apart_from_a_stranger()
    {
        // The conversation is what makes this a different case: it says there is somebody there, even
        // though the profile behind them will no longer resolve.
        RegisterClients(userJson: null, contactsJson: ContactListJson("Offline"));

        var cut = Render();

        Assert.Contains("made themselves unfindable", cut.Markup);
        Assert.DoesNotContain("nothing to show for this account", cut.Markup);
    }

    [Fact]
    public void Such_a_person_is_named_from_the_conversation_that_still_knows_them()
    {
        // A contact's name resolves without the visibility check a lookup applies, which is what keeps
        // a conversation readable - so the page has a name to show even here.
        RegisterClients(userJson: null, contactsJson: ContactListJson("Offline"));

        var cut = Render();

        Assert.Contains("Anna Kowalska", cut.Find("h1").TextContent);
    }

    [Fact]
    public void The_messages_are_said_to_be_safe()
    {
        // The part worth knowing: a door closed on the profile is not a door closed on the history.
        RegisterClients(userJson: null, contactsJson: ContactListJson("Offline"));

        var cut = Render();

        Assert.Contains("still there, and still readable", cut.Markup);
    }

    [Fact]
    public void The_conversation_can_still_be_opened_from_here()
    {
        RegisterClients(userJson: null, contactsJson: ContactListJson("Offline"));

        var cut = Render();

        Assert.NotEmpty(cut.FindAll(".contact-info-open-chat"));
    }

    [Fact]
    public void A_stranger_who_resolves_to_nothing_is_not_offered_a_conversation()
    {
        // There is nothing behind the button: no contact, no thread, nobody to write to.
        RegisterClients(userJson: null, contactsJson: "[]");

        var cut = Render();

        Assert.Empty(cut.FindAll(".contact-info-open-chat"));
    }

    [Fact]
    public void The_card_opens_the_conversation_with_them()
    {
        RegisterClients(
            userJson: $$"""{"id":"{{ContactUserId}}","userName":"anna","displayName":"Anna Kowalska","publicKeyBase64":"key"}""",
            contactsJson: ContactListJson("Available"));
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = Render();

        // An icon rather than a word now, so it is found by what it is for rather than by its text.
        cut.Find(".contact-info-open-chat").Click();

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
