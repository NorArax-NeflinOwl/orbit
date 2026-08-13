namespace Orbit.Core.Abstractions;

/// <summary>
/// Single entry point the API layer uses to run any command or query, without depending on individual handlers.
/// </summary>
public interface IDispatcher
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
