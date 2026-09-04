using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Users;
using Orbit.Web.Components;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Components;

/// <summary>
/// The other half of Manage cookies: that one is about what this browser keeps, this is about what
/// leaves it. What these hold is that the switch shows the account's own answer, that changing it
/// reaches the server, and that the browser is told so the next first paint can honour it without
/// waiting for a request.
/// </summary>
public sealed class DoNotShareDialogTests : OrbitTestContext
{
    public DoNotShareDialogTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Fact]
    public void It_opens_on_what_the_account_already_said()
    {
        RegisterAccount(keepsThirdPartiesOut: true);

        var cut = RenderComponent<DoNotShareDialog>();

        Assert.True(cut.Find(".dialog-body input[type=checkbox]").HasAttribute("checked"));
    }

    [Fact]
    public void An_account_that_has_said_nothing_opens_unticked()
    {
        RegisterAccount(keepsThirdPartiesOut: false);

        var cut = RenderComponent<DoNotShareDialog>();

        Assert.False(cut.Find(".dialog-body input[type=checkbox]").HasAttribute("checked"));
    }

    [Fact]
    public void Turning_it_on_reaches_the_account_and_the_browser()
    {
        RegisterAccount(keepsThirdPartiesOut: false);
        JSInterop.SetupVoid("OrbitMapTiles.remember", true).SetVoidResult();
        var cut = RenderComponent<DoNotShareDialog>();

        cut.Find(".dialog-body input[type=checkbox]").Change(true);

        Assert.Equal("PUT api/users/me/privacy", _lastRequest);
        // Mirrored as well as saved: a map is drawn long before an API call could come back.
        Assert.Single(JSInterop.Invocations["OrbitMapTiles.remember"]);
        Assert.Contains("Saved", cut.Find(".info").TextContent);
    }

    [Fact]
    public void A_refusal_leaves_the_box_where_the_server_still_has_it()
    {
        RegisterAccount(keepsThirdPartiesOut: false, refuseTheSave: true);
        var cut = RenderComponent<DoNotShareDialog>();

        cut.Find(".dialog-body input[type=checkbox]").Change(true);

        Assert.False(cut.Find(".dialog-body input[type=checkbox]").HasAttribute("checked"));
        Assert.Contains("Couldn't save", cut.Find(".error").TextContent);
    }

    [Fact]
    public void It_says_what_the_switch_does_and_what_it_no_longer_needs_to()
    {
        RegisterAccount(keepsThirdPartiesOut: false);

        var cut = RenderComponent<DoNotShareDialog>();

        var body = cut.Find(".dialog-body").TextContent;
        Assert.Contains("OpenStreetMap", body);
        Assert.Contains("served by Orbit itself", body);
    }

    private string? _lastRequest;

    private void RegisterAccount(bool keepsThirdPartiesOut, bool refuseTheSave = false)
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            _lastRequest = $"{request.Method} {request.RequestUri!.AbsolutePath.TrimStart('/')}";
            if (request.Method == HttpMethod.Put)
            {
                return new HttpResponseMessage(
                    refuseTheSave ? HttpStatusCode.InternalServerError : HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new AccountDto(
                    Guid.NewGuid(), "a@example.com", "anna", "Anna", IsEmailVerified: true, HasPassword: true,
                    IsGoogleLinked: false, Location: null, Availability: "Available", PresenceStatus: "Available",
                    KeepsThirdPartiesOut: keepsThirdPartiesOut))
            };
        });

        Services.AddSingleton(new UsersApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }));
    }
}
