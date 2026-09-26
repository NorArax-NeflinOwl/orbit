using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Folders;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Sharing;
using ShareAccessLevel = Orbit.Core.Abstractions.ShareAccessLevel;

namespace Orbit.Mobile.Screens.Folders;

/// <summary>
/// One thing on a list screen as choosing several of them needs to know it - which is less than a row
/// shows and more than a row carries: whose it is, whether it is sealed, and the id the server knows
/// it by.
/// </summary>
/// <param name="ServerId">Null for one the server has never seen, which cannot be offered to anybody yet.</param>
/// <param name="IsSomebodyElses">Shared with this reader rather than theirs - left out of every round, as the browser leaves it out.</param>
public sealed record PickableThing(
    Guid LocalId, Guid? ServerId, string Name, bool IsSomebodyElses, bool IsPrivate, bool IsArchived);

/// <summary>
/// What a list screen hands over for its chosen things to be acted on: one call per thing for each of
/// the two local writes, and a way to read the screen again afterwards.
/// </summary>
/// <param name="Folders">The folders somebody made on this screen - what the chosen ones can be filed into, beside "No folder".</param>
/// <param name="Redraw">Reads the screen from the phone again and asks for a sync - what every single-thing action on these screens already does afterwards.</param>
public sealed record PickingActions(
    SharedItemKind Kind,
    Func<Guid, Guid?, CancellationToken, Task<LocalWriteOutcome>> File,
    Func<Guid, bool, CancellationToken, Task<LocalWriteOutcome>> Archive,
    Func<CancellationToken, Task> Redraw,
    Func<IReadOnlyList<LocalFolder>> Folders);

/// <summary>
/// Choosing several things on a phone's list screen and doing one thing to all of them - filing them,
/// putting them away or back, or offering them to one contact. The phone's half of what the browser's
/// list pages do with PickedThingsBar, over the same rules (<see cref="PickedThings"/>, which is why that
/// class lives in Orbit.Core).
///
/// <b>Choosing is a mode here too</b>, entered from the menu under the screen's name rather than by
/// holding a row. Holding already means something inside a note (it starts choosing boxes), and a list
/// where a press that lasted a moment too long chose instead of opened would surprise the reader the
/// browser's own reasoning is about - see info/functionality.md.
///
/// One call per thing rather than a bulk write, as the browser does: each goes through the same local
/// write a single press uses, so each is queued, refused offline or applied exactly as one would be.
/// </summary>
public sealed partial class PickingSeveral : ObservableObject
{
    private readonly PickedThings _picked = new();
    private readonly Translations _translations;
    private readonly PickingActions _actions;
    private readonly SharingSeveral? _sharing;

    /// <summary>What is on the screen now, by local id - see <see cref="Shows"/>.</summary>
    private Dictionary<Guid, PickableThing> _onScreen = [];

    public PickingSeveral(Translations translations, PickingActions actions, SharingSeveral? sharing = null)
    {
        _translations = translations;
        _actions = actions;
        _sharing = sharing;
    }

    /// <summary>
    /// Raised whenever what is chosen changes, or whether the screen is choosing at all. The screen's rows
    /// are records and carry their mark as a value, so the screen redraws them on this.
    /// </summary>
    public event EventHandler? Changed;

    [ObservableProperty]
    private bool _isWorking;

    /// <summary>What the last round has to say for itself - how many were refused, or who they went to.</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    public bool IsPicking => _picked.IsPicking;

    public bool HasAny => _picked.HasAny;

    public bool Holds(Guid localId) => _picked.Holds(localId);

    /// <summary>"3 chosen" - the bar's own line.</summary>
    public string Summary => _translations.Format("{0} chosen", _picked.Count.ToString());

    public bool HasMessage => Message.Length > 0;

    /// <summary>
    /// Whether everything chosen is already away, which is what turns the one button from Archive into
    /// Put back - the browser's EveryChosenOneIsAway.
    /// </summary>
    public bool EveryOneIsAway => _picked.HasAny && Chosen().All(thing => thing.IsArchived);

    /// <summary>The name of that button, for the state it is in now.</summary>
    public string ArchiveName => EveryOneIsAway ? _translations["Put back"] : _translations["Archive"];

    /// <summary>Whether the bar offers sharing at all: only where this account may share.</summary>
    public bool CanShare => _sharing?.IsAllowed == true;

    /// <summary>What the chosen ones can be filed into - see PickingActions.Folders.</summary>
    public IReadOnlyList<LocalFolder> Folders => _actions.Folders();

    /// <summary>How much the contact may do with what they are offered - asked once for all of it.</summary>
    public IReadOnlyList<AccessLevelChoice> AccessLevels => AccessLevelChoice.All(_translations);

    /// <summary>Starts choosing, with nothing chosen yet.</summary>
    public void Start()
    {
        _picked.Start();
        Message = string.Empty;
        Say();
    }

    /// <summary>Stops choosing and forgets what was chosen - the one way out of the mode.</summary>
    [RelayCommand]
    public void Stop()
    {
        _picked.Stop();
        Message = string.Empty;
        Say();
    }

    /// <summary>
    /// A press on a row while choosing: chooses it, or unchooses it. Answers whether the press was taken
    /// - false while not choosing, when the same press opens the row as it always has.
    /// </summary>
    public bool Toggle(Guid localId)
    {
        if (!_picked.IsPicking)
        {
            return false;
        }

        _picked.Toggle(localId);
        Say();
        return true;
    }

    /// <summary>Everything on the screen, or nothing when all of it is chosen already - see PickedThings.ToggleAll.</summary>
    [RelayCommand]
    private void ToggleAll()
    {
        _picked.ToggleAll(_onScreen.Keys);
        Say();
    }

    /// <summary>
    /// Told by the screen each time it draws its rows: what is on it now. Anything chosen that is no
    /// longer there stops counting - a folder chosen from the menu, or a thing put away, leaves it behind,
    /// and a bar that went on counting it would act on something nobody can see.
    /// </summary>
    public void Shows(IEnumerable<PickableThing> onScreen)
    {
        _onScreen = onScreen.ToDictionary(thing => thing.LocalId);
        _picked.KeepOnly(_onScreen.Keys);
        Say();
    }

    /// <summary>Files every chosen thing of this reader's own into <paramref name="folderId"/>, or into none.</summary>
    [RelayCommand]
    private Task FileAsync(Guid? folderId, CancellationToken cancellationToken)
        => WriteEachAsync((thing, token) => _actions.File(thing.LocalId, folderId, token), cancellationToken);

    /// <summary>Puts every chosen thing away, or back when all of them are away already - the one button.</summary>
    [RelayCommand]
    private Task ArchiveAsync(CancellationToken cancellationToken)
    {
        var isArchived = !EveryOneIsAway;
        return WriteEachAsync((thing, token) => _actions.Archive(thing.LocalId, isArchived, token), cancellationToken);
    }

    /// <summary>
    /// Who the chosen things can be offered to. Empty, with the screen told why, when there is nobody -
    /// the page asks for a contact only when there is one to choose.
    /// </summary>
    public async Task<IReadOnlyList<LocalContact>> ContactsAsync(CancellationToken cancellationToken = default)
    {
        if (_sharing is null)
        {
            return [];
        }

        var contacts = await _sharing.ContactsAsync(cancellationToken);
        Message = contacts.Count == 0
            ? _translations["Nobody to share with yet - start a conversation first."]
            : string.Empty;
        return contacts;
    }

    /// <summary>
    /// Offers every chosen thing that can be offered to <paramref name="recipient"/>. Left out, and
    /// counted: a sealed one (the server holds nothing readable to hand over), somebody else's, and one
    /// the server has never seen. What is left out stays chosen, so the reader can see what it was.
    /// </summary>
    public async Task ShareAsync(LocalContact recipient, ShareAccessLevel accessLevel, CancellationToken cancellationToken = default)
    {
        if (_sharing is null || !_picked.HasAny || IsWorking)
        {
            return;
        }

        var chosen = Chosen();
        var offerable = chosen
            .Where(thing => !thing.IsSomebodyElses && !thing.IsPrivate && thing.ServerId is not null)
            .ToList();
        var leftOut = chosen.Count - offerable.Count;

        IsWorking = true;
        try
        {
            int shared = 0, failed = 0;
            var unreachable = false;
            foreach (var thing in offerable)
            {
                var outcome = await _sharing.ShareAsync(
                    _actions.Kind, thing.ServerId!.Value, thing.Name, recipient.UserId, accessLevel, cancellationToken);
                if (outcome is SharingOutcome.Offered or SharingOutcome.AlreadyShared)
                {
                    shared++;
                    continue;
                }

                failed++;
                unreachable |= outcome is SharingOutcome.Unreachable;
            }

            List<string> said = [];
            if (offerable.Count > 0)
            {
                said.Add(shared == 0 && unreachable
                    ? _translations["Sharing needs a connection."]
                    : failed == 0
                        ? _translations.Format("Shared {0} - they'll see them in your chat.", shared.ToString())
                        : _translations.Format("Shared {0}. {1} could not be shared.", shared.ToString(), failed.ToString()));
            }

            if (leftOut > 0)
            {
                said.Add(_translations.Format(
                    "{0} of the chosen can't be shared - private, somebody else's, or not on the server yet.",
                    leftOut.ToString()));
            }

            Message = string.Join(" ", said);
        }
        finally
        {
            IsWorking = false;
        }
    }

    /// <summary>
    /// One local write per chosen thing of this reader's own, in order, a refusal stopping nothing - the
    /// browser's OnePressEach. The screen is read again once, at the end, and what was chosen is
    /// forgotten there - see PickedThings.Forget, which says why the round has to say so itself now that
    /// filing no longer takes anything off the screen it was filed from.
    /// </summary>
    private async Task WriteEachAsync(
        Func<PickableThing, CancellationToken, Task<LocalWriteOutcome>> write, CancellationToken cancellationToken)
    {
        if (!_picked.HasAny || IsWorking)
        {
            return;
        }

        var chosen = Chosen();
        IsWorking = true;
        try
        {
            var refusedOffline = 0;
            foreach (var thing in chosen.Where(thing => !thing.IsSomebodyElses))
            {
                if (await write(thing, cancellationToken) is LocalWriteOutcome.RefusedWhileOffline)
                {
                    refusedOffline++;
                }
            }

            List<string> said = [];
            if (refusedOffline > 0)
            {
                said.Add(_translations.Format(
                    "{0} of the chosen couldn't be changed while you're offline.", refusedOffline.ToString()));
            }

            var somebodyElses = chosen.Count(thing => thing.IsSomebodyElses);
            if (somebodyElses > 0)
            {
                said.Add(_translations.Format(
                    "{0} of the chosen are somebody else's, so they were left as they are.", somebodyElses.ToString()));
            }

            Message = string.Join(" ", said);
            _picked.Forget();
            await _actions.Redraw(cancellationToken);
        }
        finally
        {
            IsWorking = false;
            Say();
        }
    }

    private List<PickableThing> Chosen()
        => [.. _picked.Ids.Where(_onScreen.ContainsKey).Select(id => _onScreen[id])];

    private void Say()
    {
        OnPropertyChanged(nameof(IsPicking));
        OnPropertyChanged(nameof(HasAny));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(EveryOneIsAway));
        OnPropertyChanged(nameof(ArchiveName));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));
}
