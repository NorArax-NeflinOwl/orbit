namespace Orbit.Mobile.Crypto;

/// <summary>Which of the key gate's situations a user is in.</summary>
public enum ChatKeyGateSituation
{
    /// <summary>This device already holds the key; there is nothing to ask for.</summary>
    AlreadyUnlocked,

    /// <summary>
    /// A Google account that never set a password. There is nothing to wrap the key with yet, so one has
    /// to be created before chat can work at all.
    /// </summary>
    SetFirstPassword,

    /// <summary>The account has a password; this device simply hasn't got the key yet.</summary>
    EnterPassword
}

/// <summary>
/// What the key gate should ask for, decided away from the screen that asks it.
///
/// Small, but the one decision in that screen worth pinning down: sending someone to "set a password"
/// when they already have one asks them to create something the server will refuse, and sending them to
/// "enter your password" when they have never had one asks for something that does not exist. Both dead
/// ends, and neither is caught by anything else - the provider below is only ever handed a password.
/// </summary>
public static class ChatKeyGate
{
    public static ChatKeyGateSituation Decide(bool deviceHoldsTheKey, bool accountHasPassword)
    {
        if (deviceHoldsTheKey)
        {
            return ChatKeyGateSituation.AlreadyUnlocked;
        }

        return accountHasPassword ? ChatKeyGateSituation.EnterPassword : ChatKeyGateSituation.SetFirstPassword;
    }

    /// <summary>
    /// Resetting sends a code by email, so an address nobody has confirmed has nowhere to send it -
    /// offering the option would lead to a code that never arrives.
    /// </summary>
    public static bool CanResetPassword(bool isEmailVerified) => isEmailVerified;
}
