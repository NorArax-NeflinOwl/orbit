using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

/// <summary>
/// The way back in for somebody who cannot sign in. The server has answered these two calls since
/// accounts existed; until now the only door to them on the web was behind the chat key gate, which
/// needs a signed-in reader - the one person who does not need it.
/// </summary>
public sealed class ForgotPasswordTests : OrbitTestContext
{
    private readonly List<(string Path, string Body)> _requests = [];
    private HttpStatusCode _resetAnswer = HttpStatusCode.NoContent;

    public ForgotPasswordTests()
    {
        Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            _requests.Add((
                request.RequestUri!.AbsolutePath,
                request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty));
            return new HttpResponseMessage(
                request.RequestUri!.AbsolutePath.EndsWith("/confirm", StringComparison.Ordinal)
                    ? _resetAnswer
                    : HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        Services.AddSingleton(new AuthApiClient(httpClient, new TokenStore(new StubJSRuntime())));
    }

    [Fact]
    public void Asking_for_a_code_says_nothing_about_whether_the_account_exists()
    {
        var cut = RenderComponent<ForgotPassword>();

        AskForACodeFor(cut, "user@example.com");

        Assert.Contains("/api/auth/password-reset", _requests.Single().Path);
        // The same sentence whoever was named, because anything else would answer "does this person
        // have an Orbit account" to anybody who asks - see RequestPasswordResetCommand.
        Assert.Contains("If that account exists", cut.Markup);
    }

    [Fact]
    public void The_code_and_a_new_password_set_it_and_lead_back_to_signing_in()
    {
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<ForgotPassword>();
        AskForACodeFor(cut, "user@example.com");

        Fill(cut, "#forgotCode", "123456");
        Fill(cut, "#forgotPassword", "new-password");
        Fill(cut, "#forgotRepeatPassword", "new-password");
        ClickSaying(cut, "Set password");

        Assert.Contains("\"newPassword\":\"new-password\"", _requests.Last().Body);
        // Signing in is what unwraps the chat key with the new password - see Login.
        Assert.EndsWith("/login", navigationManager.Uri);
    }

    [Fact]
    public void A_code_that_is_no_longer_good_says_so_instead_of_leaving_the_page()
    {
        _resetAnswer = HttpStatusCode.BadRequest;
        var navigationManager = Services.GetRequiredService<NavigationManager>();
        var cut = RenderComponent<ForgotPassword>();
        AskForACodeFor(cut, "user@example.com");

        Fill(cut, "#forgotCode", "000000");
        Fill(cut, "#forgotPassword", "new-password");
        Fill(cut, "#forgotRepeatPassword", "new-password");
        ClickSaying(cut, "Set password");

        Assert.Contains("isn't valid any more", cut.Markup);
        Assert.DoesNotContain("/login", navigationManager.Uri);
    }

    [Fact]
    public void Two_passwords_that_disagree_are_refused_before_anything_is_sent()
    {
        var cut = RenderComponent<ForgotPassword>();
        AskForACodeFor(cut, "user@example.com");
        var sentSoFar = _requests.Count;

        Fill(cut, "#forgotCode", "123456");
        Fill(cut, "#forgotPassword", "new-password");
        Fill(cut, "#forgotRepeatPassword", "something-else");
        ClickSaying(cut, "Set password");

        Assert.Contains("don't match", cut.Markup);
        Assert.Equal(sentSoFar, _requests.Count);
    }

    private static void AskForACodeFor(IRenderedComponent<ForgotPassword> cut, string identifier)
    {
        Fill(cut, "#forgotIdentifier", identifier);
        ClickSaying(cut, "Send code");
    }

    /// <summary>
    /// As a password manager fills it - an "input" and nothing else, which is the event a form here has
    /// to hear (see Login.razor).
    /// </summary>
    private static void Fill(IRenderedFragment cut, string selector, string value)
        => cut.Find(selector).Input(value);

    private static void ClickSaying(IRenderedFragment cut, string label)
        => cut.FindAll("button").First(button => button.TextContent.Contains(label, StringComparison.Ordinal)).Click();
}
