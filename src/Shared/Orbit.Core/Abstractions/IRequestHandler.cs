namespace Orbit.Core.Abstractions;

/// <summary>
/// Handles a single <typeparamref name="TRequest"/> and produces its <typeparamref name="TResponse"/>.
/// Each command or query has exactly one handler implementing this interface.
/// </summary>
public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
