using System.Diagnostics;

namespace Orbit.Api.HealthChecks;

/// <summary>Result of probing a single external dependency.</summary>
public sealed record ExternalServiceProbeResult(string Name, string Url, bool IsHealthy, string Description, long DurationMs);

/// <summary>
/// Sends a lightweight HTTP request to a configured external dependency and reports whether it
/// responded successfully. Shared by <see cref="ExternalServicesHealthCheck"/> (aggregated report) and
/// the GET /health/services/{name} endpoint (single, on-demand probe) so both use identical logic.
/// </summary>
public sealed class ExternalServiceProber(IHttpClientFactory httpClientFactory)
{
    public async Task<ExternalServiceProbeResult> ProbeAsync(ExternalServiceEndpoint endpoint, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var httpClient = httpClientFactory.CreateClient(nameof(ExternalServiceProber));
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(endpoint.TimeoutMs));
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        try
        {
            using var response = await httpClient.GetAsync(endpoint.Url, HttpCompletionOption.ResponseHeadersRead, linkedSource.Token);
            stopwatch.Stop();

            return new ExternalServiceProbeResult(
                endpoint.Name,
                endpoint.Url,
                response.IsSuccessStatusCode,
                $"Responded with {(int)response.StatusCode} {response.StatusCode}.",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            stopwatch.Stop();

            // A timeout surfaces as an OperationCanceledException even though the caller's token wasn't
            // canceled, so it needs its own message instead of the generic "operation was canceled" one.
            var description = timeoutSource.IsCancellationRequested
                ? $"Timed out after {endpoint.TimeoutMs} ms."
                : exception.Message;

            return new ExternalServiceProbeResult(endpoint.Name, endpoint.Url, false, description, stopwatch.ElapsedMilliseconds);
        }
    }
}
