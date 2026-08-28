using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Orbit.Contracts.Users;
using Orbit.Web.Pages;
using Orbit.Contracts.Config;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Orbit.Web.Tests;
using Xunit;

namespace Orbit.Web.Tests.Pages;

public sealed class RegisterTests : OrbitTestContext
{
    private readonly TokenStore _tokenStore = new(new StubJSRuntime());

    public RegisterTests()
    {
        Services.AddSingleton(_tokenStore);
        // Google sign-in is unconfigured in tests, so GoogleSignInButton renders nothing and these tests
        // stay about the password form. A StubHttpMessageHandler rather than a real HttpClient: an actual
        // (unreachable) request would add real wall-clock time to every render here.
        Services.AddSingleton(new ClientFlagsApiClient(new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ClientFlagsDto(ExceptionDetailsAllowed: false, GoogleClientId: string.Empty))
            }))
        { BaseAddress = new Uri("https://example.test/") }));


        var refreshHttpClient = new HttpClient(
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        var authenticationStateProvider = new OrbitAuthenticationStateProvider(
            _tokenStore, new TokenRefreshService(_tokenStore, refreshHttpClient));
        // Registered under both the concrete type and the base type it derives from, mirroring
        // Program.cs, so components that inject either one resolve to the same instance.
        Services.AddSingleton(authenticationStateProvider);
        Services.AddSingleton<AuthenticationStateProvider>(authenticationStateProvider);
        Services.AddAuthorizationCore();

        // Register now calls OwnEncryptionKeyProvider.UnlockOrCreateAsync after a successful sign-up, so
        // it needs to resolve here too. JSInterop.JSRuntime (bUnit's own JS interop double), not
        // Services.GetRequiredService<IJSRuntime>() - resolving a service from Services here would lock
        // the container against further registrations, since bUnit treats that as "the component tree
        // has started rendering".
        var usersApiClient = new UsersApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") });
        Services.AddSingleton(usersApiClient);
        Services.AddSingleton(new OwnEncryptionKeyProvider(JSInterop.JSRuntime, usersApiClient, authenticationStateProvider));

        // Simulates the same "crypto.subtle unavailable" condition e2eeChat.js hits outside a secure
        // context, rather than leaving the unconfigured call return null (bUnit's default "Loose" JS
        // interop mode) and crash with a NullReferenceException instead of hitting Register's own
        // best-effort catch (JSException) path. bUnit requires module loads themselves ("import") to
        // succeed - SetupModule sets up a working module handle, and the exception is raised on the
        // first call made through it instead (hasOwnPrivateKey, the first thing UnlockOrCreateAsync
        // calls on the module).
        JSInterop.SetupModule("./js/e2eeChat.js")
            .Setup<bool>("hasOwnPrivateKey", _ => true)
            .SetException(new JSException("crypto.subtle is not available in this test environment."));
    }

    [Fact]
    public void Submitting_a_new_account_navigates_to_the_dashboard()
    {
        RegisterAuthApiClient(_ => JsonResponse(CreateAuthResponse("new@example.com", "New User")));
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<Register>();
        cut.Find("#email").Change("new@example.com");
        cut.Find("#userName").Change("newuser");
        cut.Find("#displayName").Change("New User");
        cut.Find("#password").Change("s3cret-password");
        cut.Find("#repeatPassword").Change("s3cret-password");
        cut.Find("form").Submit();

        Assert.EndsWith("/", navigationManager.Uri);
        // Asserted separately because the dashboard's own URL is the base URL: EndsWith("/") alone is
        // just as true when the form never submitted at all.
        Assert.Empty(cut.FindAll(".error"));
    }

    [Theory]
    [InlineData("EmailTaken", "An account with this email address already exists.")]
    [InlineData("UserNameTaken", "This login is already taken.")]
    public void Submitting_a_taken_email_or_username_says_which_one_was_taken(string reason, string expectedMessage)
    {
        RegisterAuthApiClient(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = JsonContent.Create(new RegistrationConflictDto(reason, "taken"))
        });

        var cut = RenderComponent<Register>();
        cut.Find("#email").Change("taken@example.com");
        cut.Find("#userName").Change("takenname");
        cut.Find("#displayName").Change("Someone");
        cut.Find("#password").Change("password");
        cut.Find("#repeatPassword").Change("password");
        cut.Find("form").Submit();

        // Refusing is only half the job: the reader has to learn which of the two fields to change.
        Assert.Contains(expectedMessage, cut.Markup);
    }

    [Fact]
    public void Submitting_two_different_passwords_shows_an_error_without_calling_the_api()
    {
        var apiWasCalled = false;
        RegisterAuthApiClient(_ =>
        {
            apiWasCalled = true;
            return JsonResponse(CreateAuthResponse("new@example.com", "New User"));
        });

        var cut = RenderComponent<Register>();
        cut.Find("#email").Change("new@example.com");
        cut.Find("#userName").Change("newuser");
        cut.Find("#displayName").Change("New User");
        cut.Find("#password").Change("s3cret-password");
        cut.Find("#repeatPassword").Change("s3cret-passwrod");
        cut.Find("form").Submit();

        Assert.Contains("The two passwords don't match.", cut.Markup);
        Assert.False(apiWasCalled);
    }

    private void RegisterAuthApiClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(respond)) { BaseAddress = new Uri("https://example.test/") };
        Services.AddSingleton(new AuthApiClient(httpClient, _tokenStore));
    }

    /// <summary>
    /// The access token has to be a real JWT with a "sub" claim, not just any string - Register now reads
    /// it right back out via OwnEncryptionKeyProvider.UnlockOrCreateAsync (see the constructor above) to
    /// know which user's key to unlock, and OrbitAuthenticationStateProvider.ParseClaimsFromJwt throws on
    /// anything that isn't dot-separated the way a real token is.
    /// </summary>
    private static AuthResponse CreateAuthResponse(string email, string displayName)
    {
        var userId = Guid.NewGuid();
        var token = CreateUnsignedJwt(new Dictionary<string, string> { ["sub"] = userId.ToString() });
        return new AuthResponse(token, "a-refresh-token", userId, email, displayName);
    }

    /// <summary>
    /// Builds a JWT with a real header and payload but a dummy signature - enough to exercise the
    /// client's own claim-parsing logic, which never checks the signature (the server already did, on
    /// every API call that carries this token).
    /// </summary>
    private static string CreateUnsignedJwt(Dictionary<string, string> claims)
    {
        var header = Base64UrlEncode(Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT"}"""));
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(claims));
        return $"{header}.{payload}.";
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static HttpResponseMessage JsonResponse(AuthResponse body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}