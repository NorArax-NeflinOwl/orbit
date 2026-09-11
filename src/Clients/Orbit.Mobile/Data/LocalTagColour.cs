namespace Orbit.Mobile.Data;

/// <summary>
/// One of the account's tag colours as this phone holds it - see Orbit.Core.Tags.TagColour. Every screen
/// reads the colours from here, so a card is coloured with no connection; a colour chosen here is written
/// here first and sent from here, so one picked on a train is kept and arrives later (<see cref="IsPending"/>).
/// </summary>
public sealed class LocalTagColour
{
    /// <summary>The tag as TagNames.KeyOf folds it - the key, as it is on the server, so "Work" and "work" share one.</summary>
    public string NormalizedTag { get; set; } = string.Empty;

    /// <summary>The tag as it was written when its colour was last set, for sending.</summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>"#rrggbb", or empty for a colour taken away that the server has not been told about yet.</summary>
    public string Colour { get; set; } = string.Empty;

    /// <summary>
    /// Set on this phone and not sent yet. A pull leaves such a row alone - it is newer than anything the
    /// server has - and the sync sends it first.
    /// </summary>
    public bool IsPending { get; set; }
}
