using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orbit.Api.Notifications;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

/// <summary>
/// Covers the sender against a stand-in for a real push service. What was untestable here was never the
/// VAPID signing or the RFC 8291 encryption - the WebPush package owns both - but everything Orbit
/// wrapped around them: whether an unconfigured deployment stays quiet, and whether a subscription the
/// push service has given up on comes back as the one exception the dispatcher prunes on.
/// </summary>
public sealed class VapidPushNotificationSenderTests
{
    private static readonly PushNotificationPayload Payload = new("A reminder", "It starts in ten minutes", "/calendar");

    [Fact]
    public async Task An_unconfigured_deployment_sends_nothing_and_says_so()
    {
        var context = new VapidTestContext(Configured: false);

        await context.SendAsync(WebPushSubscription());

        // A fresh checkout with no VAPID keys still has to run. Dropping the notification is the
        // deliberate choice - see VapidSettings.IsConfigured - and it is only defensible if it is said
        // out loud rather than silently.
        Assert.Empty(context.Requests);
        Assert.Contains(context.Logger.Entries, entry => entry.Message.Contains("Vapid is not configured"));
    }

    [Fact]
    public async Task A_configured_deployment_posts_to_the_endpoint_the_browser_handed_out()
    {
        var context = new VapidTestContext(Configured: true);

        await context.SendAsync(WebPushSubscription());

        var request = Assert.Single(context.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://push.example.test/send/abc", request.Url.ToString());
        // The body is the encrypted payload, not the text - the push service is no more able to read
        // this than Orbit's own server is able to read a chat message.
        Assert.DoesNotContain("It starts in ten minutes", request.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task A_subscription_the_push_service_has_given_up_on_comes_back_as_expired(HttpStatusCode statusCode)
    {
        var context = new VapidTestContext(Configured: true, Answer: statusCode);

        // The one failure the dispatcher is allowed to prune a stored subscription on, which is why it
        // has to be told apart from every other way a delivery can fail.
        await Assert.ThrowsAsync<PushSubscriptionExpiredException>(() => context.SendAsync(WebPushSubscription()));
    }

    [Fact]
    public async Task A_push_service_having_a_bad_day_is_not_mistaken_for_an_expired_subscription()
    {
        var context = new VapidTestContext(Configured: true, Answer: HttpStatusCode.ServiceUnavailable);

        // Pruning on this would throw away a working subscription because the push service was briefly
        // down, and the user would silently stop receiving notifications for good.
        await Assert.ThrowsAnyAsync<Exception>(() => context.SendAsync(WebPushSubscription()));
        await Assert.ThrowsAsync<WebPush.WebPushException>(() => context.SendAsync(WebPushSubscription()));
    }

    [Fact]
    public async Task A_row_claiming_web_push_without_a_registration_is_reported_rather_than_sent()
    {
        var context = new VapidTestContext(Configured: true);
        var corrupt = PushSubscription.FromPersistence(
            Guid.NewGuid(), Guid.NewGuid(), PushTransport.WebPush, webPush: null, device: null, DateTimeOffset.UtcNow);

        await context.SendAsync(corrupt);

        Assert.Empty(context.Requests);
        Assert.Contains(context.Logger.Entries, entry => entry.Message.Contains("carries no registration"));
    }

    /// <summary>
    /// A subscription with keys that are really on the P-256 curve. Generated rather than pasted in:
    /// RFC 8291 derives the message encryption from the browser's public key, so an invented one is
    /// rejected inside the WebPush package long before a request is made - and a pasted one would be a
    /// constant nobody could regenerate if it ever needed changing.
    /// </summary>
    private static PushSubscription WebPushSubscription()
    {
        using var browserKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var authSecret = RandomNumberGenerator.GetBytes(16);

        return PushSubscription.CreateForBrowser(
            Guid.NewGuid(),
            new WebPushRegistration(
                "https://push.example.test/send/abc",
                Base64Url(UncompressedPoint(browserKey.ExportParameters(includePrivateParameters: false))),
                Base64Url(authSecret)));
    }

    /// <summary>The 0x04||X||Y form both VAPID and RFC 8291 exchange public keys in.</summary>
    private static byte[] UncompressedPoint(ECParameters parameters)
        => [0x04, .. parameters.Q.X!, .. parameters.Q.Y!];

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// The sender wired to a push service that never existed. Configured is a parameter rather than two
    /// contexts because it is the single thing the "stays quiet" cases vary.
    /// </summary>
    private sealed record VapidTestContext(bool Configured, HttpStatusCode Answer = HttpStatusCode.Created)
    {
        public RecordingLogger<VapidPushNotificationSender> Logger { get; } = new();

        /// <summary>
        /// What the push service was asked, kept here rather than read off the request afterwards -
        /// HttpClient disposes a request's content the moment the call returns.
        /// </summary>
        public List<RecordedRequest> Requests { get; } = [];

        public StubHttpMessageHandler Handler => new(async (request, cancellationToken) =>
        {
            Requests.Add(new RecordedRequest(
                request.Method,
                request.RequestUri!,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(Answer);
        });

        public Task SendAsync(PushSubscription subscription)
        {
            var handler = Handler;
            var settings = Configured ? ConfiguredVapidSettings() : new VapidSettings();

            var sender = new VapidPushNotificationSender(
                new TestOptionsMonitor<VapidSettings>(settings),
                new WebPush.WebPushClient(new HttpClient(handler)),
                Logger);

            return sender.SendAsync(subscription, Payload, CancellationToken.None);
        }

        /// <summary>
        /// A freshly generated VAPID key pair, for the same reason the browser's key is generated: a
        /// hard-coded one is a constant with no owner, and this one is only ever signed with.
        /// </summary>
        private static VapidSettings ConfiguredVapidSettings()
        {
            using var signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var parameters = signingKey.ExportParameters(includePrivateParameters: true);

            return new VapidSettings
            {
                Subject = "mailto:orbit@example.test",
                PublicKeyBase64Url = Base64Url(UncompressedPoint(parameters)),
                PrivateKeyBase64Url = Base64Url(parameters.D!)
            };
        }

        /// <summary>One request as it left Orbit, kept whole so a test can assert on where it went and what it carried.</summary>
        public sealed record RecordedRequest(HttpMethod Method, Uri Url, string Body);
    }
}
