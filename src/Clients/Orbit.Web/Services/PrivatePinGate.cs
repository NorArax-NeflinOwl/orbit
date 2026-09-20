using System.Text.Json;
namespace Orbit.Web.Services;

/// <summary>
/// Whether this reader has answered the PIN yet - the door in front of what is private (asked for on
/// 2026-09-20). One PIN per account, asked once per session.
///
/// **It is a door, not a lock.** What keeps a private note unreadable is the key it is sealed with,
/// which is the account's own and never reaches the server (see <see cref="PrivateContentSealer"/>).
/// This is the question asked before what has already been unsealed is drawn on a screen somebody else
/// might be standing at - so it belongs in memory and nowhere else: a tab that is closed and opened
/// again asks again, which is the whole of "once per session".
///
/// Deliberately *not* in localStorage or sessionStorage. A remembered answer is a door somebody left
/// open, and the point of the question is the screen in front of it.
///
/// The map's points are not behind it, and that is the user's own line: a place is met on a map that is
/// read at a glance, and a question in front of it would be asked at every one.
/// </summary>
public sealed class PrivatePinGate
{
    private readonly UsersApiClient _usersApiClient;

    public PrivatePinGate(UsersApiClient usersApiClient)
    {
        _usersApiClient = usersApiClient;
    }

    /// <summary>Raised when the answer changes, so a page drawing what is private can draw it.</summary>
    public event Action? Changed;

    /// <summary>
    /// Whether the account has one at all, as the last read of the account said. Null until something
    /// has asked - see <see cref="LearnWhetherThereIsOneAsync"/> - and treated as "no door" while it is,
    /// because a page that hid what is private until an answer arrived would flicker on every load.
    /// </summary>
    public bool? HasOne { get; private set; }

    /// <summary>Whether the question has been answered in this session, or there is nothing to answer.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    /// Whether a page should put the question up before drawing what is private. False while nothing is
    /// known yet, for the same reason <see cref="HasOne"/> is null then.
    /// </summary>
    public bool Asks => HasOne == true && !IsOpen;

    /// <summary>
    /// Takes the account's own answer to "is there a PIN" from whatever has just read the account, so
    /// the gate does not read it a second time. An account with none leaves the door open.
    /// </summary>
    public void LearnWhetherThereIsOne(bool hasOne)
    {
        HasOne = hasOne;
        if (!hasOne)
        {
            IsOpen = true;
        }

        Changed?.Invoke();
    }

    /// <inheritdoc cref="LearnWhetherThereIsOne"/>
    public async Task LearnWhetherThereIsOneAsync(CancellationToken cancellationToken = default)
    {
        if (HasOne is not null)
        {
            return;
        }

        try
        {
            var account = await _usersApiClient.GetAccountAsync(cancellationToken);
            LearnWhetherThereIsOne(account?.HasPrivatePin == true);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            // A question nobody can be asked is not a question: an account that could not be read - or
            // came back as something this build cannot read - leaves the door as it was, and the pages
            // carry on drawing what they were drawing.
        }
    }

    /// <summary>
    /// Answers it. True opens the door for the rest of this session; false leaves it shut and is what
    /// the dialog says out loud. Asked of the server rather than compared here - the PIN is stored
    /// hashed and nothing on this side has it.
    /// </summary>
    public async Task<bool> AnswerAsync(string pin, CancellationToken cancellationToken = default)
    {
        if (!await _usersApiClient.VerifyPrivatePinAsync(pin, cancellationToken))
        {
            return false;
        }

        IsOpen = true;
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Shuts it again, and forgets whether there is one - for the reader who has just set, changed or
    /// taken away their PIN, and for signing out. A gate that remembered an answer across accounts
    /// would be a door opened by somebody else's key.
    /// </summary>
    public void Forget()
    {
        HasOne = null;
        IsOpen = false;
        Changed?.Invoke();
    }
}
