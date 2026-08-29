namespace Orbit.Mobile.Authentication;

/// <summary>
/// What happened to an account operation. There is no queued outcome here on purpose - see
/// <see cref="AccountClient"/>.
/// </summary>
public enum AccountOperationStatus
{
    Applied,

    /// <summary>
    /// Refused before anything was sent, because the phone has no connection. Never queued: the user is
    /// told to try again once they are online.
    /// </summary>
    RequiresConnection,

    /// <summary>
    /// The server considered it and said no - a wrong password, or a username or email address that
    /// belongs to somebody else. Only the server can know either.
    /// </summary>
    Refused
}

/// <summary>
/// The outcome, with whatever the server said about it. <paramref name="Message"/> is null when there
/// is nothing to add beyond the status.
/// </summary>
public sealed record AccountOperationResult(AccountOperationStatus Status, string? Message = null)
{
    public static AccountOperationResult Applied { get; } = new(AccountOperationStatus.Applied);

    public static AccountOperationResult RequiresConnection { get; } = new(
        AccountOperationStatus.RequiresConnection,
        "This needs a connection to Orbit. Try again when you're back online.");

    public static AccountOperationResult Refused(string message) => new(AccountOperationStatus.Refused, message);

    public bool Succeeded => Status is AccountOperationStatus.Applied;
}
