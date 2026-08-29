namespace Orbit.Mobile.Data;

/// <summary>
/// Whose data the phone's database currently holds.
///
/// One row, rewritten on every sign-in. It exists because signing out is not the only way a session
/// ends: an expired token drops the reader at the sign-in screen with no chance to clear anything, and
/// the next person to sign in would otherwise open somebody else's notes, calendar and decrypted
/// messages. Comparing this on sign-in is what makes that impossible rather than merely unlikely.
/// </summary>
public sealed class LocalStoreOwner
{
    /// <summary>Always 1: there is one database and it belongs to one account at a time.</summary>
    public int Id { get; set; } = 1;

    public Guid UserId { get; set; }
}
