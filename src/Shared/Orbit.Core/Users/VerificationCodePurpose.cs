namespace Orbit.Core.Users;

/// <summary>What a <see cref="UserVerificationCode"/> entitles its holder to do once they prove they received it.</summary>
public enum VerificationCodePurpose
{
    /// <summary>
    /// Proves control of an email address. Confirming one both marks the account's email verified and
    /// *switches* the account to that address, which is how changing an email works: the new address is
    /// never written to the account until someone proves they can read mail at it, so a typo can't hand
    /// the account - and every future password reset - to an address the user doesn't own.
    /// </summary>
    EmailVerification,

    /// <summary>Lets the holder set a new password without knowing the current one - only ever sent to an already-verified address.</summary>
    PasswordReset
}
