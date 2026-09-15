using Orbit.Contracts;
using Orbit.Core;

namespace Orbit.Api;

/// <summary>
/// Stamps every response as Orbit's own - see <see cref="OrbitAnswerHeader"/> for what a client does
/// with that and why it needs to.
///
/// First in the pipeline, ahead of the rate limiter, authentication and the exception handler, so a 429,
/// a 401 and a 500 are stamped the same as a 200: the point is that <i>any</i> answer from Orbit can be
/// told from one the platform gave in its place, and a refusal is the case that matters most. Written on
/// starting rather than added up front, because a header appended before an endpoint replaces the
/// response is lost with it.
///
/// The value is the build, which costs nothing and answers "what am I talking to" for anyone reading the
/// headers - the same number the config endpoint and the footer show.
/// </summary>
public static class AnswerHeader
{
    public static IApplicationBuilder UseAnswerHeader(this IApplicationBuilder app)
    {
        var version = OrbitVersion.ReadFrom(typeof(AnswerHeader).Assembly).Version;

        return app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[OrbitAnswerHeader.Name] = version;
                return Task.CompletedTask;
            });

            return next();
        });
    }
}
