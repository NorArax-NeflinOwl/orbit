namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// Another list an entry can be moved into. Only lists the server knows about: moving needs both real
/// ids, so a list that has never synced is not somewhere anything can go yet.
/// </summary>
public sealed record TaskListChoice(Guid ServerId, string Name);
