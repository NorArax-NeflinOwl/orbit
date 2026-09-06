namespace Orbit.Api.LiveUpdates;

/// <summary>
/// Delivery to the connections *this* instance is holding, and nothing further.
///
/// A separate name from <see cref="ILiveUpdateFanOut"/> because the two are not interchangeable, even
/// though one extends the other. <see cref="PostgresLiveUpdateFanOut"/> is registered as the general
/// one and needs the local one underneath it; asking for the general one there would resolve to itself.
/// Naming the narrower role says which is wanted and makes the cycle impossible rather than merely
/// avoided by whoever wires it up.
/// </summary>
public interface ILocalLiveUpdateFanOut : ILiveUpdateFanOut;
