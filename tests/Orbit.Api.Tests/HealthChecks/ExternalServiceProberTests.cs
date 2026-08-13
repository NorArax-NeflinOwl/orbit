using System.Net;
using Orbit.Api.HealthChecks;
using Orbit.Api.Tests.TestDoubles;
using Xunit;

namespace Orbit.Api.Tests.HealthChecks;

public sealed class ExternalServiceProberTests
{
    [Fact]
    public async Task ProbeAsync_returns_healthy_result_for_a_successful_response()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var prober = new ExternalServiceProber(new StubHttpClientFactory(handler));
        var endpoint = new ExternalServiceEndpoint { Name = "healthy-service", Url = "https://example.test/", TimeoutMs = 5000 };

        var result = await prober.ProbeAsync(endpoint, CancellationToken.None);

        Assert.True(result.IsHealthy);
        Assert.Equal("healthy-service", result.Name);
    }

    [Fact]
    public async Task ProbeAsync_returns_unhealthy_result_for_an_error_status_code()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var prober = new ExternalServiceProber(new StubHttpClientFactory(handler));
        var endpoint = new ExternalServiceEndpoint { Name = "failing-service", Url = "https://example.test/", TimeoutMs = 5000 };

        var result = await prober.ProbeAsync(endpoint, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains("500", result.Description);
    }

    [Fact]
    public async Task ProbeAsync_returns_unhealthy_result_when_the_request_throws()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("Name or service not known"));
        var prober = new ExternalServiceProber(new StubHttpClientFactory(handler));
        var endpoint = new ExternalServiceEndpoint { Name = "unreachable-service", Url = "https://example.test/", TimeoutMs = 5000 };

        var result = await prober.ProbeAsync(endpoint, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Equal("Name or service not known", result.Description);
    }

    [Fact]
    public async Task ProbeAsync_returns_unhealthy_result_when_the_request_times_out()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var prober = new ExternalServiceProber(new StubHttpClientFactory(handler));
        var endpoint = new ExternalServiceEndpoint { Name = "slow-service", Url = "https://example.test/", TimeoutMs = 50 };

        var result = await prober.ProbeAsync(endpoint, CancellationToken.None);

        Assert.False(result.IsHealthy);
        Assert.Contains("Timed out", result.Description);
    }
}
