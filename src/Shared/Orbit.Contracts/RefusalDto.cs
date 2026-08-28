namespace Orbit.Contracts;

/// <summary>
/// The body of a refused request - see InvalidRequestExceptionHandler, which writes exactly this shape
/// for every 400 the API raises. Named as a contract rather than read as an anonymous object, so the
/// two ends cannot drift apart quietly.
/// </summary>
public sealed record RefusalDto(string Message);
