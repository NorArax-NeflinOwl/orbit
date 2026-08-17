using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Contracts.Users;
using Orbit.Web.Pages;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Pages;

public sealed class RegisterTests : TestContext
{
    private readonly TokenStore _tokenStore = new(new StubJSRuntime());

    public RegisterTests()
    {
        Services.AddSingleton(_tokenStore);
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(_tokenStore);
        // Registered under both the concrete type and the base type it derives from, mirroring
        // Program.cs, so components that inject either one resolve to the same instance.
        Services.AddSingleton(authenticationStateProvider);
        Services.AddSingleton<AuthenticationStateProvider>(authenticationStateProvider);
        Services.AddAuthorizationCore();
    }

    [Fact]
    public void Submitting_a_new_account_navigates_to_the_dashboard()
    {
        RegisterAuthApiClient(_ => JsonResponse(new AuthResponse("a-token", "a-refresh-token", Guid.NewGuid(), "new@example.com", "New User")));
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<Register>();
        cut.Find("#email").Change("new@example.com");
        cut.Find("#userName").Change("newuser");
        cut.Find("#displayName").Change("New User");
        cut.Find("#password").Change("s3cret-password");
        cut.Find("form").Submit();

        Assert.EndsWith("/", navigationManager.Uri);
    }

    [Fact]
    public void Submitting_an_email_or_username_that_is_already_taken_shows_a_polish_error_message()
    {
        RegisterAuthApiClient(_ => new HttpResponseMessage(HttpStatusCode.Conflict));

        var cut = RenderComponent<Register>();
        cut.Find("#email").Change("taken@example.com");
        cut.Find("#userName").Change("takenname");
        cut.Find("#displayName").Change("Someone");
        cut.Find("#password").Change("password");
        cut.Find("form").Submit();

        Assert.Contains("Ten adres e-mail lub login jest już zajęty.", cut.Markup);
    }

    private void RegisterAuthApiClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(respond)) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new AuthApiClient(httpClient, _tokenStore));
    }

    private static HttpResponseMessage JsonResponse(AuthResponse body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
