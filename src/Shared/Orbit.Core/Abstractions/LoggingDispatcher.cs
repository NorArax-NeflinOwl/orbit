using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Orbit.Core.Abstractions;

/// <summary>
/// Decorator pattern: wraps the real <see cref="Dispatcher"/> so every command and query dispatched
/// through the application is logged and timed in exactly one place. New handlers get tracing and
/// timing automatically, without remembering to add it themselves.
///
/// Requests decorated with <see cref="ClientActionAttribute"/> additionally get an "[ACTION:{Category}]"
/// prefix on their log lines, so a client-triggered action (save, login, share, ...) stands out from
/// routine/background dispatches when scanning or grepping the log stream.
/// </summary>
public sealed class LoggingDispatcher : IDispatcher
{
    private static readonly ActivitySource ActivitySource = new("Orbit.Core");
    private static readonly ConcurrentDictionary<Type, ClientActionCategory?> ClientActionCategoriesByRequestType = new();

    private readonly IDispatcher _inner;
    private readonly ILogger<LoggingDispatcher> _logger;

    public LoggingDispatcher(IDispatcher inner, ILogger<LoggingDispatcher> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var requestName = requestType.Name;
        var actionCategory = GetClientActionCategory(requestType);
        using var activity = ActivitySource.StartActivity(requestName, ActivityKind.Internal);
        var stopwatch = Stopwatch.StartNew();

        LogStarted(requestName, actionCategory);

        try
        {
            var response = await _inner.SendAsync(request, cancellationToken);
            stopwatch.Stop();
            LogCompleted(requestName, actionCategory, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            LogFailed(requestName, actionCategory, stopwatch.ElapsedMilliseconds, exception);
            throw;
        }
    }

    /// <summary>Reflection result is cached per request type, since it never changes at runtime and
    /// SendAsync runs on every dispatch.</summary>
    private static ClientActionCategory? GetClientActionCategory(Type requestType)
        => ClientActionCategoriesByRequestType.GetOrAdd(
            requestType, static type => type.GetCustomAttribute<ClientActionAttribute>()?.Category);

    private void LogStarted(string requestName, ClientActionCategory? actionCategory)
    {
        if (actionCategory is null)
        {
            _logger.LogInformation("{RequestName} started", requestName);
            return;
        }

        _logger.LogInformation("[ACTION:{ActionCategory}] {RequestName} started", actionCategory, requestName);
    }

    private void LogCompleted(string requestName, ClientActionCategory? actionCategory, long elapsedMilliseconds)
    {
        if (actionCategory is null)
        {
            _logger.LogInformation("{RequestName} completed in {ElapsedMilliseconds} ms", requestName, elapsedMilliseconds);
            return;
        }

        _logger.LogInformation(
            "[ACTION:{ActionCategory}] {RequestName} completed in {ElapsedMilliseconds} ms",
            actionCategory, requestName, elapsedMilliseconds);
    }

    /// <summary>
    /// An <see cref="InvalidRequestException"/> is the caller being told no - a private note that can't
    /// be shared, a link that would make a cycle between task lists - and the API turns it into a 400.
    /// That is expected input rather than a fault, so it logs as a warning: left at Error, the Error
    /// level fills up with ordinary refusals and stops meaning that something is actually wrong, which
    /// is the only thing it is for. Every other exception is still an error.
    /// </summary>
    private void LogFailed(string requestName, ClientActionCategory? actionCategory, long elapsedMilliseconds, Exception exception)
    {
        var level = exception is InvalidRequestException ? LogLevel.Warning : LogLevel.Error;

        if (actionCategory is null)
        {
            _logger.Log(level, exception, "{RequestName} failed after {ElapsedMilliseconds} ms", requestName, elapsedMilliseconds);
            return;
        }

        _logger.Log(
            level, exception, "[ACTION:{ActionCategory}] {RequestName} failed after {ElapsedMilliseconds} ms",
            actionCategory, requestName, elapsedMilliseconds);
    }
}
