namespace Orbit.Mobile.Crypto;

/// <summary>
/// This device holds no usable chat key for the signed-in user, and getting one needs the account
/// password - which is only ever on hand right after signing in (see
/// <see cref="OwnEncryptionKeyProvider.UnlockOrCreateAsync"/>).
///
/// Not a transient failure: retrying achieves nothing. The user has to supply their password, which on
/// the web is what Chat's password gate is for.
/// </summary>
public sealed class EncryptionKeyLockedException : Exception
{
    public EncryptionKeyLockedException()
        : base("This device has no chat encryption key for the signed-in user.")
    {
    }
}
