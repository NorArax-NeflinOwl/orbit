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
/// The PIN in front of what is private (asked for 2026-09-20). It is a door rather than a lock: what
/// keeps a sealed note unreadable is the key it is sealed with. This is the question asked before what
/// has already been unsealed is drawn on a screen somebody else might be standing at - so what is
/// behind it is not rendered at all while it is unanswered, rather than drawn and covered over.
/// </summary>
public sealed class BehindThePinTests : OrbitTestContext
{
    private const string Secret = "The diary";

    public BehindThePinTests() => Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    /// <param name="hasPin">What the account says about itself - see AccountDto.HasPrivatePin.</param>
    /// <param name="rightPin">The one the server will accept; anything else is refused.</param>
    private void RegisterAccount(bool hasPin, string rightPin = "1234")
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/private-pin/check", StringComparison.Ordinal))
            {
                var offered = request.Content!.ReadFromJsonAsync<VerifyPrivatePinRequest>()
                    .GetAwaiter().GetResult()!;
                return new HttpResponseMessage(
                    offered.Pin == rightPin ? HttpStatusCode.NoContent : HttpStatusCode.Unauthorized);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new AccountDto(
                    Guid.NewGuid(), "a@example.com", "anna", "Anna", IsEmailVerified: true,
                    HasPassword: true, IsGoogleLinked: false, Location: null,
                    Availability: "Available", PresenceStatus: "Available",
                    KeepsThirdPartiesOut: false, HasPrivatePin: hasPin))
            };
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new UsersApiClient(httpClient));
    }

    private IRenderedComponent<BehindThePin> Render(bool applies = true)
        => RenderComponent<BehindThePin>(parameters => parameters
            .Add(gate => gate.Applies, applies)
            .AddChildContent($"<p>{Secret}</p>"));

    [Fact]
    public void An_account_with_no_pin_is_asked_nothing()
    {
        RegisterAccount(hasPin: false);

        var cut = Render();

        Assert.Contains(Secret, cut.Markup);
    }

    /// <summary>
    /// And what is not private is drawn without the account being asked anything at all. Most of what
    /// this wraps is an ordinary page or an ordinary form: a network call between the reader and those
    /// is a beat of nothing, paid on every one of them.
    /// </summary>
    [Fact]
    public void What_is_not_private_is_drawn_whatever_the_account_has_set()
    {
        RegisterAccount(hasPin: true);

        var cut = Render(applies: false);

        Assert.Contains(Secret, cut.Markup);
        Assert.Null(Services.GetRequiredService<PrivatePinGate>().HasOne);
    }

    /// <summary>
    /// Nothing behind it is rendered while the question stands. A page that drew what is private and
    /// put a panel over it would already have put it on the screen, and the screen is what the question
    /// is about.
    /// </summary>
    [Fact]
    public void What_is_private_is_not_drawn_until_the_pin_is_given()
    {
        RegisterAccount(hasPin: true);

        var cut = Render();

        Assert.DoesNotContain(Secret, cut.Markup);
        Assert.Contains("Type your PIN", cut.Markup);
    }

    [Fact]
    public void The_right_pin_opens_it()
    {
        RegisterAccount(hasPin: true, rightPin: "1234");
        var cut = Render();

        Answer(cut, "1234");

        Assert.Contains(Secret, cut.Markup);
    }

    /// <summary>A wrong one says so and keeps the door shut - and empties the box for the next try.</summary>
    [Fact]
    public void A_wrong_pin_says_so_and_changes_nothing()
    {
        RegisterAccount(hasPin: true, rightPin: "1234");
        var cut = Render();

        Answer(cut, "9999");

        Assert.DoesNotContain(Secret, cut.Markup);
        Assert.Contains("That is not the PIN", cut.Markup);
        Assert.Equal(string.Empty, cut.Find("#privatePinBox").GetAttribute("value"));
    }

    /// <summary>
    /// And once answered it stays answered for the rest of the session - one gate per tab, which is
    /// what "once per session" means in a WebAssembly app. A second thing drawn behind it asks nothing.
    /// </summary>
    [Fact]
    public void It_is_asked_once_and_not_again()
    {
        RegisterAccount(hasPin: true, rightPin: "1234");
        Answer(Render(), "1234");

        var second = Render();

        Assert.Contains(Secret, second.Markup);
    }

    private static void Answer(IRenderedComponent<BehindThePin> cut, string pin)
    {
        cut.Find("#privatePinBox").Input(pin);
        cut.FindAll("button").First(button => button.TextContent.Trim() == "Unlock").Click();
    }
}
