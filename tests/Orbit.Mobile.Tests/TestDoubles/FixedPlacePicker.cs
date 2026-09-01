using Orbit.Mobile.Location;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// The map answering however the test says, which no test can arrange for real - the real one opens a
/// map over the screen and waits for a tap.
/// </summary>
internal sealed class FixedPlacePicker : IPlacePicker
{
    public PickedPlace Result { get; set; } = PickedPlace.Chosen("12 Mill Lane", 52.23, 21.01);

    /// <summary>What the box held when the map was opened, so a test can check it opens where it should.</summary>
    public string? StartedAt { get; private set; }

    public int PickCount { get; private set; }

    public Task<PickedPlace> PickAsync(string startingAddress, CancellationToken cancellationToken = default)
    {
        PickCount++;
        StartedAt = startingAddress;
        return Task.FromResult(Result);
    }
}
