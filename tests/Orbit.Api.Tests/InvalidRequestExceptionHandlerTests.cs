using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Api;
using Orbit.Core.Abstractions;
using Xunit;

namespace Orbit.Api.Tests;

/// <summary>
/// Covers the single place that decides what a refused request looks like, and - just as importantly -
/// what it refuses to claim: a fault in Orbit's own code must not be handed back to the caller as their
/// mistake.
/// </summary>
public sealed class InvalidRequestExceptionHandlerTests
{
    [Fact]
    public async Task A_refused_request_becomes_a_400_carrying_the_reason()
    {
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var exception = new InvalidRequestException("An event's end time can't be before its start time.");

        var handled = await CreateHandler().TryHandleAsync(httpContext, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal(exception.Message, await ReadMessageAsync(httpContext));
    }

    [Fact]
    public async Task A_plain_argument_exception_is_left_alone()
    {
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        // InvalidRequestException derives from ArgumentException, but not the other way round: this is
        // the shape of a bug in Orbit passing something impossible, and answering "your request was
        // wrong" would send the caller hunting for a mistake they never made. It stays a 500.
        var handled = await CreateHandler().TryHandleAsync(
            httpContext, new ArgumentException("Value cannot be null. (Parameter 'value')"), CancellationToken.None);

        Assert.False(handled);
        Assert.NotEqual(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    [Fact]
    public async Task An_unrelated_failure_is_left_alone()
    {
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        var handled = await CreateHandler().TryHandleAsync(
            httpContext, new InvalidOperationException("The database went away."), CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    private static InvalidRequestExceptionHandler CreateHandler()
        => new(NullLogger<InvalidRequestExceptionHandler>.Instance);

    private static async Task<string?> ReadMessageAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(httpContext.Response.Body);
        return document.RootElement.GetProperty("message").GetString();
    }
}
