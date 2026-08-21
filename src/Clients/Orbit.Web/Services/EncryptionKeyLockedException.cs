namespace Orbit.Web.Services;

/// <summary>
/// Thrown by <see cref="OwnEncryptionKeyProvider.EnsurePublicKeyAsync"/> when this browser has no local
/// copy of the signed-in user's E2EE private key - most often because it was generated in a different
/// browser or profile, or this one's storage was cleared. The only fix is signing in again (see
/// OwnEncryptionKeyProvider.UnlockOrCreateAsync), since restoring the key needs the account password,
/// which is only ever available right after a fresh login or registration.
/// </summary>
public sealed class EncryptionKeyLockedException : Exception
{
}
