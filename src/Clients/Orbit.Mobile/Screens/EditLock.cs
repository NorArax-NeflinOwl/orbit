using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens;

/// <summary>
/// Holds a shared item for as long as an editor is open on it, and says who has it when somebody else
/// does.
///
/// Without this the phone edits shared things with nothing claimed. <see cref="OfflineEditPolicy"/>
/// already says that online "the server's locks are the authority" - but only a client that asks for
/// one is under that authority, and until now the phone never asked. The cost was quiet: a change made
/// while somebody else held the lock was refused at sync time and abandoned with only a log line.
///
/// The claim lapses server-side after a minute, so it is refreshed on a heartbeat while the editor
/// stays open and dropped when it closes. A phone that is put in a pocket mid-edit therefore frees the
/// item on its own, which is the behaviour worth having.
/// </summary>
public sealed class EditLock
{
    /// <summary>Comfortably inside the server's own minute - see AcquireNoteLockCommandHandler.</summary>
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    private readonly INetworkStatus _networkStatus;
    private readonly TimeProvider _timeProvider;
    private readonly Translations _translations;

    /// <summary>What is being held, and by what. Null between a release and the next hold.</summary>
    private Held? _held;

    public EditLock(INetworkStatus networkStatus, TimeProvider timeProvider, Translations translations)
    {
        _networkStatus = networkStatus;
        _timeProvider = timeProvider;
        _translations = translations;
    }

    /// <summary>Raised when somebody else takes the item out from under an open editor.</summary>
    public event EventHandler? Changed;

    /// <summary>Who else is in it, or null when nobody is.</summary>
    public string? HeldByOtherUserName { get; private set; }

    public bool IsHeldByAnother => HeldByOtherUserName is not null;

    /// <summary>The line to show the reader when somebody else is in it.</summary>
    public string RefusalMessage
        => IsHeldByAnother
            ? _translations.Format("{0} is editing this right now - it stays read-only until they finish.", HeldByOtherUserName)
            : string.Empty;

    /// <summary>
    /// Claims the item, and keeps claiming it until <see cref="ReleaseAsync"/>. Returns false when
    /// somebody else has it, which is the one answer the editor has to act on.
    ///
    /// Offline there is nobody to ask and nothing to hold; the offline policy decides that case on its
    /// own, from what the phone already knows.
    /// </summary>
    public async Task<bool> HoldAsync(ILockableItems items, Guid serverId, CancellationToken cancellationToken = default)
    {
        await ReleaseAsync();

        if (!_networkStatus.IsOnline)
        {
            return true;
        }

        var claim = await AskAsync(items, serverId, cancellationToken);
        HeldByOtherUserName = claim.HeldByOtherUserName;

        if (claim.IsHeldByAnother)
        {
            return false;
        }

        _held = new Held(items, serverId, new CancellationTokenSource());
        _ = KeepHoldingAsync(_held);
        return true;
    }

    /// <summary>Lets go, if anything is held. Safe to call when nothing is.</summary>
    public async Task ReleaseAsync()
    {
        if (_held is not { } held)
        {
            HeldByOtherUserName = null;
            return;
        }

        _held = null;
        HeldByOtherUserName = null;
        await held.Heartbeat.CancelAsync();
        held.Heartbeat.Dispose();

        try
        {
            await held.Items.ReleaseLockAsync(held.ServerId, CancellationToken.None);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // It expires on its own within the minute.
        }
    }

    private async Task KeepHoldingAsync(Held held)
    {
        using var timer = new PeriodicTimer(HeartbeatInterval, _timeProvider);

        while (await timer.WaitForNextTickAsync(held.Heartbeat.Token))
        {
            var claim = await AskAsync(held.Items, held.ServerId, held.Heartbeat.Token);
            if (!claim.IsHeldByAnother)
            {
                continue;
            }

            // Taken out from under an open editor - only possible if this claim lapsed first, which
            // means the phone was away long enough for the server to give it to somebody else.
            HeldByOtherUserName = claim.HeldByOtherUserName;
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }
    }

    private static async Task<EditClaim> AskAsync(
        ILockableItems items, Guid serverId, CancellationToken cancellationToken)
    {
        try
        {
            return await items.AcquireLockAsync(serverId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return EditClaim.Free;
        }
    }

    /// <summary>What one hold consists of - the three travel together and are dropped together.</summary>
    private sealed record Held(ILockableItems Items, Guid ServerId, CancellationTokenSource Heartbeat);
}
