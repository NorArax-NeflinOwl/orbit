using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orbit.Api.HealthChecks;
using Orbit.Api.Tests.TestDoubles;
using Xunit;

namespace Orbit.Api.Tests.HealthChecks;

public sealed class HealthEndpointsTests
{
    [Fact]
    public async Task WriteHealthReportAsync_writes_the_report_as_json()
    {
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["database"] = new HealthReportEntry(
                HealthStatus.Healthy, "Database connection succeeded.", TimeSpan.FromMilliseconds(5), exception: null, data: null)
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(12));
        var context = CreateHttpContext(out var responseBody);

        await HealthEndpoints.WriteHealthReportAsync(context, report);

        Assert.Equal("application/json", context.Response.ContentType);
        responseBody.Position = 0;
        using var document = await JsonDocument.ParseAsync(responseBody);
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
        var checks = document.RootElement.GetProperty("checks");
        Assert.Equal(1, checks.GetArrayLength());
        Assert.Equal("database", checks[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task CheckSingleExternalServiceAsync_returns_not_found_for_an_unknown_service_name()
    {
        var settings = new HealthCheckSettings { ExternalServices = new ExternalServicesHealthCheckSettings { Services = [] } };
        var prober = new ExternalServiceProber(new StubHttpClientFactory(
            new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("Should not probe an unknown service."))));

        var result = await HealthEndpoints.CheckSingleExternalServiceAsync(
            "unknown-service", new TestOptionsMonitor<HealthCheckSettings>(settings), prober, CancellationToken.None);

        var context = CreateHttpContext(out _);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task CheckSingleExternalServiceAsync_probes_and_returns_the_result_for_a_known_service()
    {
        var settings = new HealthCheckSettings
        {
            ExternalServices = new ExternalServicesHealthCheckSettings
            {
                Services =
                [
                    new ExternalServiceEndpoint { Name = "push-notifications", Url = "https://example.test/", Enabled = false, TimeoutMs = 5000 }
                ]
            }
        };
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var prober = new ExternalServiceProber(new StubHttpClientFactory(handler));

        var result = await HealthEndpoints.CheckSingleExternalServiceAsync(
            "push-notifications", new TestOptionsMonitor<HealthCheckSettings>(settings), prober, CancellationToken.None);

        var context = CreateHttpContext(out var responseBody);
        await result.ExecuteAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("push-notifications", body);
    }

    private static DefaultHttpContext CreateHttpContext(out MemoryStream responseBody)
    {
        responseBody = new MemoryStream();
        // Ok<T>/NotFound<T>.ExecuteAsync resolve ILoggerFactory from RequestServices to log the response,
        // so an empty provider isn't enough; AddLogging() registers a no-op factory that satisfies it.
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = responseBody;
        return context;
    }
}
