using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.PushNotifications;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

public sealed class PushNotificationApiClientTests
{
    [Fact]
    public async Task GetVapidPublicKeyAsync_returns_the_key_from_the_response_body()
    {
        var client = new PushNotificationApiClient(
            CreateHttpClient(_ => JsonResponse(new PushPublicKeyDto("a-public-key"))));

        var publicKey = await client.GetVapidPublicKeyAsync();

        Assert.Equal("a-public-key", publicKey);
    }

    [Fact]
    public async Task SubscribeAsync_posts_the_endpoint_and_keys()
    {
        HttpRequestMessage? capturedRequest = null;
        PushSubscriptionRequest? capturedBody = null;
        var client = new PushNotificationApiClient(CreateHttpClient(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content!.ReadFromJsonAsync<PushSubscriptionRequest>().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

        await client.SubscribeAsync("https://push.example/a", "p256dh", "auth");

        Assert.Equal("api/push/subscriptions", capturedRequest!.RequestUri!.PathAndQuery.TrimStart('/'));
        Assert.Equal("https://push.example/a", capturedBody!.Endpoint);
        Assert.Equal("p256dh", capturedBody.P256dhBase64);
        Assert.Equal("auth", capturedBody.AuthBase64);
    }

    [Fact]
    public async Task UnsubscribeAsync_sends_the_endpoint_as_a_query_parameter()
    {
        HttpRequestMessage? capturedRequest = null;
        var client = new PushNotificationApiClient(CreateHttpClient(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

        await client.UnsubscribeAsync("https://push.example/a");

        Assert.Equal(HttpMethod.Delete, capturedRequest!.Method);
        Assert.Contains("endpoint=https%3A%2F%2Fpush.example%2Fa", capturedRequest.RequestUri!.Query);
    }

    [Fact]
    public async Task UnsubscribeAsync_treats_a_not_found_response_as_success()
    {
        var client = new PushNotificationApiClient(CreateHttpClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));

        await client.UnsubscribeAsync("https://push.example/already-gone");
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new(new StubHttpMessageHandler(respond)) { BaseAddress = new Uri("https://example.test/") };

    private static HttpResponseMessage JsonResponse<T>(T body)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
}
