namespace Orbit.Core.Abstractions;

/// <summary>
/// Marks an operation that can be sent through the <see cref="IDispatcher"/> and produces a <typeparamref name="TResponse"/>.
/// Both commands and queries implement this so they share a single dispatch and logging path.
/// </summary>
public interface IRequest<TResponse>
{
}
