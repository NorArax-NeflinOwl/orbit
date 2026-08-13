using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orbit.Api.HealthChecks;
using Orbit.Api.Tests.TestDoubles;
using Xunit;

namespace Orbit.Api.Tests.HealthChecks;

public sealed class ExternalServicesHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_returns_healthy_without_probing_when_disabled()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("Should not be called while the check is disabled."));
        var settings = new HealthCheckSettings
        {
            ExternalServices = new ExternalServicesHealthCheckSettings
            {
                Enabled = false,
                Services = [new ExternalServiceEndpoint { Name = "some-service", Url = "https://example.test/", Enabled = true }]
            }
        };

        var result = await CreateHealthCheck(handler, settings).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_no_services_are_configured()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("Should not be called; no services are configured."));
        var settings = new HealthCheckSettings
        {
            ExternalServices = new ExternalServicesHealthCheckSettings { Enabled = true, Services = [] }
        };

        var result = await CreateHealthCheck(handler, settings).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_skips_services_disabled_individually()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
            request.RequestUri!.ToString().Contains("disabled-service")
                ? throw new InvalidOperationException("A disabled service must not be probed.")
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var settings = new HealthCheckSettings
        {
            ExternalServices = new ExternalServicesHealthCheckSettings
            {
                Enabled = true,
                Services =
                [
                    new ExternalServiceEndpoint { Name = "enabled-service", Url = "https://example.test/enabled-service", Enabled = true },
                    new ExternalServiceEndpoint { Name = "disabled-service", Url = "https://example.test/disabled-service", Enabled = false }
                ]
            }
        };

        var result = await CreateHealthCheck(handler, settings).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_unhealthy_when_one_service_fails_to_respond()
    {
        var handler = new StubHttpMessageHandler((request, _) => Task.FromResult(new HttpResponseMessage(
            request.RequestUri!.ToString().Contains("failing") ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)));
        var settings = new HealthCheckSettings
        {
            ExternalServices = new ExternalServicesHealthCheckSettings
            {
                Enabled = true,
                Services =
                [
                    new ExternalServiceEndpoint { Name = "healthy-service", Url = "https://example.test/healthy", Enabled = true },
                    new ExternalServiceEndpoint { Name = "failing-service", Url = "https://example.test/failing", Enabled = true }
                ]
            }
        };

        var result = await CreateHealthCheck(handler, settings).CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    private static ExternalServicesHealthCheck CreateHealthCheck(HttpMessageHandler handler, HealthCheckSettings settings)
    {
        var prober = new ExternalServiceProber(new StubHttpClientFactory(handler));
        return new ExternalServicesHealthCheck(prober, new TestOptionsMonitor<HealthCheckSettings>(settings));
    }
}
