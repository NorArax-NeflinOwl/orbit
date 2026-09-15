using Orbit.Contracts;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Lets an answer through only if Orbit gave it, and turns any other into
/// <see cref="AnswerNotFromOrbitException"/> - the innermost handler on every client that talks to
/// Orbit, so that <see cref="Authentication.AuthorizationMessageHandler"/> and every caller above it
/// only ever see statuses the API itself chose.
///
/// The test is the header <see cref="OrbitAnswerHeader"/> stamps on every response. Its absence is the
/// platform speaking in Orbit's place - a Container Apps front door answering 404 for a stopped app -
/// and reading that as a refusal is what signed every phone out within fifteen minutes of a cost stop.
/// The reachability is told either way, so a pause can be noticed and can end.
/// </summary>
public sealed class OrbitAnswerHandler : DelegatingHandler
{
    private readonly ServerReachability _reachability;

    public OrbitAnswerHandler(ServerReachability reachability) => _reachability = reachability;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.Contains(OrbitAnswerHeader.Name))
        {
            _reachability.RecordAnswerFromOrbit();
            return response;
        }

        var answeredWith = response.StatusCode;
        response.Dispose();
        await _reachability.RecordAnswerNotFromOrbitAsync(cancellationToken);
        throw new AnswerNotFromOrbitException(answeredWith, request.RequestUri);
    }
}
