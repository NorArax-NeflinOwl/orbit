using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using Orbit.Contracts.PushNotifications;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// The decisions PushNotificationManager makes between the browser and Orbit.Api. The browser half of
/// this - the permission prompt, the subscription itself - is covered separately by
/// ci/verify-push-notifications.mjs, which runs pushNotifications.js and service-worker.js in a real
/// browser; what is left here is which of those calls happen, in what order, and what reaches the server
/// afterwards.
///
/// Worth pinning because both mistakes are quiet ones: registering a subscription the browser never made
/// leaves the server pushing into nothing, and failing to register one it did make leaves a person who
/// said yes to notifications receiving none.
/// </summary>
public sealed class PushNotificationManagerTests
{
    /// <summary>
    /// Nothing is asked of the browser when the server has no key to subscribe against. Permission is
    /// asked for once and remembered, so spending it on a subscription that cannot work would leave the
    /// person unable to say yes again later without going into browser settings.
    /// </summary>
    [Fact]
    public async Task Enabling_without_a_key_on_the_server_does_not_prompt_the_browser()
    {
        var browser = new StubPushModule();
        var manager = ManagerFor(browser, VapidKeyOf(""));

        var enabled = await manager.EnableAsync();

        Assert.False(enabled);
        Assert.Empty(browser.Calls);
    }

    /// <summary>The three parts the server needs to reach this browser, exactly as the browser gave them.</summary>
    [Fact]
    public async Task Enabling_registers_what_the_browser_subscribed_with()
    {
        PushSubscriptionRequest? registered = null;
        var browser = new StubPushModule
        {
            Subscription = new
            {
                endpoint = "https://push.example/abc",
                p256dhBase64 = "a-public-key",
                authBase64 = "an-auth-secret",
            },
        };
        var manager = ManagerFor(browser, request =>
        {
            if (request.Method != HttpMethod.Post)
            {
                return JsonResponse(new PushPublicKeyDto("a-vapid-key"));
            }

            registered = request.Content!.ReadFromJsonAsync<PushSubscriptionRequest>().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var enabled = await manager.EnableAsync();

        Assert.True(enabled);
        Assert.Equal("https://push.example/abc", registered!.Endpoint);
        Assert.Equal("a-public-key", registered.P256dhBase64);
        Assert.Equal("an-auth-secret", registered.AuthBase64);
        Assert.Equal("a-vapid-key", Assert.Single(browser.Calls, call => call.Name == "requestPermissionAndSubscribe").FirstArgument);
    }

    /// <summary>
    /// Somebody who refuses the prompt has no subscription, and the server must not be told otherwise:
    /// a registration with no browser behind it is one the sender keeps pushing to for as long as it stands.
    /// </summary>
    [Fact]
    public async Task A_refused_prompt_registers_nothing()
    {
        var requests = new List<HttpRequestMessage>();
        var browser = new StubPushModule { Subscription = null };
        var manager = ManagerFor(browser, request =>
        {
            requests.Add(request);
            return JsonResponse(new PushPublicKeyDto("a-vapid-key"));
        });

        var enabled = await manager.EnableAsync();

        Assert.False(enabled);
        Assert.DoesNotContain(requests, request => request.Method == HttpMethod.Post);
    }

    /// <summary>Turning it off has to reach the server too, or it stops only on this device.</summary>
    [Fact]
    public async Task Disabling_tells_the_server_which_endpoint_went()
    {
        HttpRequestMessage? deletion = null;
        var browser = new StubPushModule { UnsubscribedEndpoint = "https://push.example/abc" };
        var manager = ManagerFor(browser, request =>
        {
            if (request.Method == HttpMethod.Delete)
            {
                deletion = request;
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await manager.DisableAsync();

        Assert.Contains("push.example", deletion!.RequestUri!.Query);
    }

    /// <summary>
    /// With nothing to cancel there is nothing to tell the server about. The endpoint is what identifies
    /// the subscription, and a call with an empty one would ask the server to delete by no endpoint at all.
    /// </summary>
    [Fact]
    public async Task Disabling_a_browser_that_was_never_subscribed_calls_nobody()
    {
        var requests = new List<HttpRequestMessage>();
        var browser = new StubPushModule { UnsubscribedEndpoint = null };
        var manager = ManagerFor(browser, request =>
        {
            requests.Add(request);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        await manager.DisableAsync();

        Assert.Empty(requests);
    }

    /// <summary>An endpoint on file is what "enabled" means here - there is no separate flag to trust.</summary>
    [Theory]
    [InlineData("https://push.example/abc", true)]
    [InlineData(null, false)]
    public async Task Whether_it_is_on_follows_the_browsers_own_subscription(string? endpoint, bool expected)
    {
        var manager = ManagerFor(new StubPushModule { ExistingEndpoint = endpoint }, VapidKeyOf("a-vapid-key"));

        Assert.Equal(expected, await manager.IsEnabledAsync());
    }

    /// <summary>
    /// Every import is released. Each one hands back a handle the browser holds open until it is
    /// disposed, and the control that drives this can be pressed as often as somebody likes.
    /// </summary>
    [Fact]
    public async Task Each_use_of_the_browser_module_releases_it()
    {
        var browser = new StubPushModule { ExistingEndpoint = null, UnsubscribedEndpoint = null };
        var manager = ManagerFor(browser, VapidKeyOf(""));

        await manager.IsSupportedAsync();
        await manager.IsEnabledAsync();
        await manager.DisableAsync();

        Assert.Equal(3, browser.TimesReleased);
    }

    private static PushNotificationManager ManagerFor(
        StubPushModule browser, Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new(
            new StubModuleImportingJSRuntime(browser),
            new PushNotificationApiClient(
                new HttpClient(new StubHttpMessageHandler(respond)) { BaseAddress = new Uri("https://example.test/") }));

    private static Func<HttpRequestMessage, HttpResponseMessage> VapidKeyOf(string publicKey)
        => _ => JsonResponse(new PushPublicKeyDto(publicKey));

    private static HttpResponseMessage JsonResponse<T>(T body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    /// <summary>Answers the one import PushNotificationManager makes, and nothing else.</summary>
    private sealed class StubModuleImportingJSRuntime : IJSRuntime
    {
        private readonly StubPushModule _pushModule;

        public StubModuleImportingJSRuntime(StubPushModule pushModule)
        {
            _pushModule = pushModule;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier != "import" || args?[0] as string != "./js/pushNotifications.js")
            {
                throw new NotSupportedException($"Nothing but the push module is expected here, got '{identifier}'.");
            }

            return ValueTask.FromResult((TValue)(object)_pushModule);
        }
    }

    /// <summary>
    /// Stands in for wwwroot/js/pushNotifications.js. Answers travel through JSON the way a real interop
    /// call's would, which is also how the private shape the manager deserialises into is reached from
    /// out here.
    /// </summary>
    private sealed class StubPushModule : IJSObjectReference
    {
        public List<(string Name, object? FirstArgument)> Calls { get; } = [];

        public int TimesReleased { get; private set; }

        public bool IsSupported { get; init; } = true;

        public string? ExistingEndpoint { get; init; }

        public object? Subscription { get; init; }

        public string? UnsubscribedEndpoint { get; init; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add((identifier, args?.FirstOrDefault()));

            object? answer = identifier switch
            {
                "isSupported" => IsSupported,
                "getPermissionState" => "granted",
                "getExistingSubscriptionEndpoint" => ExistingEndpoint,
                "requestPermissionAndSubscribe" => Subscription,
                "unsubscribe" => UnsubscribedEndpoint,
                _ => throw new NotSupportedException($"pushNotifications.js has no '{identifier}'.")
            };

            return ValueTask.FromResult(As<TValue>(answer));
        }

        public ValueTask DisposeAsync()
        {
            TimesReleased++;
            return ValueTask.CompletedTask;
        }

        /// <summary>The same web defaults Blazor's own interop marshals with - camelCase, case-insensitive.</summary>
        private static readonly JsonSerializerOptions AsInterop = new(JsonSerializerDefaults.Web);

        private static TValue As<TValue>(object? answer)
            => answer is null or TValue
                ? (TValue)answer!
                : JsonSerializer.Deserialize<TValue>(JsonSerializer.Serialize(answer, AsInterop), AsInterop)!;
    }
}
