namespace Orbit.Mobile.Presence;

/// <summary>
/// Remembers the reader's chosen availability across launches.
///
/// Worth persisting, unlike the verbosity switch next door: "do not disturb" is a deliberate decision
/// somebody makes once, and an app that quietly returns to available on the next launch would announce
/// them exactly when they asked it not to.
/// </summary>
public interface IPresenceStore
{
    ChosenAvailability Read();

    void Write(ChosenAvailability availability);
}
