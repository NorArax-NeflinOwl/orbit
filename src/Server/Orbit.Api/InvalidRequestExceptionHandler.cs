using Microsoft.AspNetCore.Diagnostics;
using Orbit.Core.Abstractions;

namespace Orbit.Api;

/// <summary>
/// Turns every <see cref="InvalidRequestException"/> into a 400 carrying its message, wherever in the
/// request it was raised - domain validation, an endpoint reading a value by name, anything else.
/// Registered once in Program.cs rather than caught per endpoint, so refusing a request reads the same
/// way across the whole API and no endpoint can quietly forget to do it.
///
/// Anything else is left alone (returns false), which keeps a genuine fault a 500: reporting a bug in
/// Orbit as "your request was wrong" would send the caller looking for a mistake they didn't make.
/// </summary>
public sealed class InvalidRequestExceptionHandler : IExceptionHandler
{
    private readonly ILogger<InvalidRequestExceptionHandler> _logger;

    public InvalidRequestExceptionHandler(ILogger<InvalidRequestExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not InvalidRequestException invalidRequest)
        {
            return false;
        }

        // Information rather than error: a refused request is the API working as intended, and these
        // would otherwise fill the log with stack traces every time someone sends something invalid.
        _logger.LogInformation(
            "Refused {Method} {Path}: {Reason}", httpContext.Request.Method, httpContext.Request.Path, invalidRequest.Message);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(new { message = invalidRequest.Message }, cancellationToken);
        return true;
    }
}
