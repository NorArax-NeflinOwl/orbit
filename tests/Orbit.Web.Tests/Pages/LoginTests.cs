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

        // Login now calls OwnEncryptionKeyProvider.UnlockOrCreateAsync after a successful sign-in, so
        // it needs to resolve here too. JSInterop.JSRuntime (bUnit's own JS interop double), not
        // Services.GetRequiredService<IJSRuntime>() - resolving a service from Services here would lock
        // the container against further registrations, since bUnit treats that as "the component tree
        // has started rendering".
        var usersApiClient = new UsersApiClient(new HttpClient { BaseAddress = new Uri("https://example.test/") });
        Services.AddSingleton(usersApiClient);
        Services.AddSingleton(new OwnEncryptionKeyProvider(JSInterop.JSRuntime, usersApiClient, authenticationStateProvider));

        // Simulates the same "crypto.subtle unavailable" condition e2eeChat.js hits outside a secure
        // context, rather than leaving the unconfigured call return null (bUnit's default "Loose" JS
        // interop mode) and crash with a NullReferenceException instead of hitting Login's own
        // best-effort catch (JSException) path. bUnit requires module loads themselves ("import") to
        // succeed - SetupModule sets up a working module handle, and the exception is raised on the
        // first call made through it instead (hasOwnPrivateKey, the first thing UnlockOrCreateAsync
        // calls on the module).
        JSInterop.SetupModule("./js/e2eeChat.js")
            .Setup<bool>("hasOwnPrivateKey", _ => true)
            .SetException(new JSException("crypto.subtle is not available in this test environment."));
    }

    [Fact]
    public void Submitting_a_valid_email_navigates_to_the_dashboard()
    {
        RegisterAuthApiClient(_ => JsonResponse(CreateAuthResponse("user@example.com", "User")));
        var navigationManager = Services.GetRequiredService<NavigationManager>();

        var cut = RenderComponent<Login>();
        cut.Find("#emailOrUserName").Change("user@example.com");
        cut.Find("#password").Change("correct-password");
        cut.Find("form").Submit();

        Assert.EndsWith("/", navigationManager.Uri);
    }

    [Fact]
    public void Submitting_a_valid_username_navigates_to_the_dashboard()
    {
        RegisterAuthApiClient(_ => JsonResponse(CreateAuthResponse("user@example.com", "User")));
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

    /// <summary>
    /// The access token has to be a real JWT with a "sub" claim, not just any string - Login now reads it
    /// right back out via OwnEncryptionKeyProvider.UnlockOrCreateAsync (see the constructor above) to know
    /// which user's key to unlock, and OrbitAuthenticationStateProvider.ParseClaimsFromJwt throws on
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