using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Orbit.Api;
using Orbit.Contracts;
using Xunit;

namespace Orbit.Api.Tests;

/// <summary>
/// Every answer from Orbit carries the header the phone takes as proof that Orbit answered - a refusal
/// included, since a stopped app's front door also refuses, and telling the two apart is the whole point.
/// </summary>
public sealed class AnswerHeaderTests
{
    [Fact]
    public async Task Every_response_is_stamped_as_Orbits_own()
    {
        var httpContext = await RunThroughAsync(context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return context.Response.WriteAsync("ok");
        });

        Assert.True(httpContext.Response.Headers.ContainsKey(OrbitAnswerHeader.Name));
        Assert.NotEmpty(httpContext.Response.Headers[OrbitAnswerHeader.Name].ToString());
    }

    [Fact]
    public async Task A_refusal_is_stamped_the_same_as_a_success()
    {
        var httpContext = await RunThroughAsync(context =>
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return context.Response.WriteAsync(string.Empty);
        });

        Assert.Equal(StatusCodes.Status404NotFound, httpContext.Response.StatusCode);
        Assert.True(httpContext.Response.Headers.ContainsKey(OrbitAnswerHeader.Name));
    }

    /// <summary>
    /// A DefaultHttpContext does not run OnStarting callbacks on its own - a server does, when the first
    /// byte goes out - so the endpoint here writes a body, and the header is asserted after the pipeline
    /// has fired the callback the way a server would.
    /// </summary>
    private static async Task<HttpContext> RunThroughAsync(RequestDelegate endpoint)
    {
        var app = new ApplicationBuilder(new ServiceProviderStub());
        app.UseAnswerHeader();
        app.Run(endpoint);
        var pipeline = app.Build();

        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var startingCallbacks = new List<Func<Task>>();
        httpContext.Features.Set<IHttpResponseFeature>(new StartingAwareResponseFeature(httpContext, startingCallbacks));

        await pipeline(httpContext);
        foreach (var callback in startingCallbacks)
        {
            await callback();
        }

        return httpContext;
    }

    /// <summary>Records what the middleware asked to run on starting, which the default feature drops.</summary>
    private sealed class StartingAwareResponseFeature : IHttpResponseFeature
    {
        private readonly IHttpResponseFeature _inner;
        private readonly List<Func<Task>> _startingCallbacks;

        public StartingAwareResponseFeature(HttpContext httpContext, List<Func<Task>> startingCallbacks)
        {
            _inner = httpContext.Features.Get<IHttpResponseFeature>()!;
            _startingCallbacks = startingCallbacks;
        }

        public int StatusCode { get => _inner.StatusCode; set => _inner.StatusCode = value; }
        public string? ReasonPhrase { get => _inner.ReasonPhrase; set => _inner.ReasonPhrase = value; }
        public IHeaderDictionary Headers { get => _inner.Headers; set => _inner.Headers = value; }
        // The interface still requires it, obsolete or not; this only passes it through.
#pragma warning disable CS0618
        public Stream Body { get => _inner.Body; set => _inner.Body = value; }
#pragma warning restore CS0618
        public bool HasStarted => _inner.HasStarted;

        public void OnStarting(Func<object, Task> callback, object state) => _startingCallbacks.Add(() => callback(state));

        public void OnCompleted(Func<object, Task> callback, object state) => _inner.OnCompleted(callback, state);
    }

    private sealed class ServiceProviderStub : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
