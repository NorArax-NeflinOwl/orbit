using Microsoft.Extensions.DependencyInjection;

namespace Orbit.Core.Abstractions;

/// <summary>
/// Resolves the <see cref="IRequestHandler{TRequest,TResponse}"/> matching the runtime type of the request
/// and invokes it. Kept dependency-free (no MediatR) since a handful of handlers don't need assembly scanning.
/// </summary>
public sealed class Dispatcher : IDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public Dispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        dynamic handler = _serviceProvider.GetRequiredService(handlerType);

        return handler.HandleAsync((dynamic)request, cancellationToken);
    }
}
