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

public sealed class LoginTests : TestContext
{
    private readonly TokenStore _tokenStore = new(new StubJSRuntime());

    public LoginTests()
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
    public void Submitting_a_valid_email_navigates_to_the_notes_page()
    {
        RegisterAuthApiClient(_ => JsonResponse(new AuthResponse("a-token", "a-refresh-token", Guid.NewGuid(), "user@example.com", "User")));
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<Login>();
        cut.Find("#emailOrUserName").Change("user@example.com");
        cut.Find("#password").Change("correct-password");
        cut.Find("form").Submit();

        Assert.EndsWith("/", navigationManager.Uri);
    }

    [Fact]
    public void Submitting_a_valid_username_navigates_to_the_notes_page()
    {
        RegisterAuthApiClient(_ => JsonResponse(new AuthResponse("a-token", "a-refresh-token", Guid.NewGuid(), "user@example.com", "User")));
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<Login>();
        cut.Find("#emailOrUserName").Change("username");
        cut.Find("#password").Change("correct-password");
        cut.Find("form").Submit();

        Assert.EndsWith("/", navigationManager.Uri);
    }

    [Fact]
    public void Submitting_invalid_credentials_shows_a_polish_error_message()
    {
        RegisterAuthApiClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var cut = RenderComponent<Login>();
        cut.Find("#emailOrUserName").Change("user@example.com");
        cut.Find("#password").Change("wrong-password");
        cut.Find("form").Submit();

        Assert.Contains("Nieprawidłowy e-mail, login lub hasło.", cut.Markup);
    }

    private void RegisterAuthApiClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(respond)) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new AuthApiClient(httpClient, _tokenStore));
    }

    private static HttpResponseMessage JsonResponse(AuthResponse body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
