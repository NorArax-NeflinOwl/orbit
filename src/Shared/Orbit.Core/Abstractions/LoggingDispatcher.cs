using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Orbit.Core.Abstractions;

/// <summary>
/// Decorator pattern: wraps the real <see cref="Dispatcher"/> so every command and query dispatched
/// through the application is logged and timed in exactly one place. New handlers get tracing and
/// timing automatically, without remembering to add it themselves.
/// </summary>
public sealed class LoggingDispatcher : IDispatcher
{
    private static readonly ActivitySource ActivitySource = new("Orbit.Core");

    private readonly IDispatcher _inner;
    private readonly ILogger<LoggingDispatcher> _logger;

    public LoggingDispatcher(IDispatcher inner, ILogger<LoggingDispatcher> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var requestName = request.GetType().Name;
        using var activity = ActivitySource.StartActivity(requestName, ActivityKind.Internal);
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("{RequestName} started", requestName);

        try
        {
            var response = await _inner.SendAsync(request, cancellationToken);
            stopwatch.Stop();
            _logger.LogInformation(
                "{RequestName} completed in {ElapsedMilliseconds} ms", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            _logger.LogError(
                exception, "{RequestName} failed after {ElapsedMilliseconds} ms", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
