namespace Orbit.Contracts;

/// <summary>
/// The header every answer from Orbit's API carries, and the one thing a client may take as proof that
/// Orbit answered at all.
///
/// The address the phone talks to is a wildcard on a Container Apps environment. With the app stopped -
/// which the cost limits make a routine, days-long state - the address still resolves and the
/// environment's front door answers 404 for a host no app claims. Read as the API's opinion, that
/// status signed every phone out within fifteen minutes and had the outbox discarding queued edits by
/// the evening. The front door does not set this header; only Orbit does, on every response including
/// its own refusals - so its absence means "somebody else answered", which is offline in everything
/// but the network. See info/orbit-maui-plan.md, "Living without the server".
/// </summary>
public static class OrbitAnswerHeader
{
    public const string Name = "Orbit-Api";
}
