using Orbit.Core.Abstractions;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// Minimal <see cref="IDispatcher"/> stub whose SendAsync either returns the response type's default
/// value or throws a canned exception, so <see cref="LoggingDispatcher"/> can be tested on its own
/// logging behavior without a real handler behind it.
/// </summary>
internal sealed class StubDispatcher : IDispatcher
{
    private readonly Exception? _exceptionToThrow;

    private StubDispatcher(Exception? exceptionToThrow)
    {
        _exceptionToThrow = exceptionToThrow;
    }

    public static StubDispatcher ReturningDefault() => new(exceptionToThrow: null);

    public static StubDispatcher Throwing(Exception exception) => new(exception);

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        return Task.FromResult(default(TResponse)!);
    }
}
