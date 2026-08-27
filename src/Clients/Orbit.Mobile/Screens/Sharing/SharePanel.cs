using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Abstractions;
using Orbit.Core.Permissions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Permissions;

namespace Orbit.Mobile.Screens.Sharing;

/// <summary>One level of access, named in the reader's language rather than by its enum member.</summary>
public sealed record AccessLevelChoice(ShareAccessLevel Value, string Name);

/// <summary>
/// Offering the thing on screen to somebody else. One panel shared by the note, task-list, event and
/// warehouse editors, because the question is the same on all four - who, and how much - and only what
/// is being offered differs.
///
/// Held by each editor rather than being a screen of its own: sharing is something done to the thing you
/// are looking at, and a separate page would make you leave it to do so.
/// </summary>
public sealed partial class SharePanel : ObservableObject
{
    private readonly ChatRepository _chatRepository;
    private readonly SharedItemSharing _sharing;
    private readonly PublicShareClient _links;
    private readonly UserPermissions _permissions;
    private readonly Translations _translations;

    private SharedItemKind _kind;
    private Guid _itemId;
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private LocalContact? _recipient;

    [ObservableProperty]
    private AccessLevelChoice? _accessLevel;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isSharing;

    public SharePanel(
        ChatRepository chatRepository, SharedItemSharing sharing, PublicShareClient links,
        UserPermissions permissions, Translations translations)
    {
        _chatRepository = chatRepository;
        _sharing = sharing;
        _links = links;
        _permissions = permissions;
        _translations = translations;
        AccessLevels = [.. Enum.GetValues<ShareAccessLevel>().Select(level => new AccessLevelChoice(level, Describe(level)))];
        AccessLevel = AccessLevels[0];
    }

    /// <summary>People this account has a conversation with, which is who it can share with.</summary>
    public ObservableCollection<LocalContact> Recipients { get; } = [];

    public IReadOnlyList<AccessLevelChoice> AccessLevels { get; }

    /// <summary>Whether the editor should offer sharing at all - see ApplicationPermission.Sharing.</summary>
    public bool CanShare => _permissions.Has(ApplicationPermission.Sharing);

    public bool HasMessage => Message.Length > 0;

    /// <summary>The panel folded away, which is when the button that opens it is the thing on screen.</summary>
    public bool IsClosed => !IsOpen;

    /// <summary>
    /// The link anyone can read this by, or empty when there is none. A different kind of sharing from
    /// offering a copy: nobody accepts it, and whoever holds it can read without an Orbit account.
    /// </summary>
    [ObservableProperty]
    private string _linkAddress = string.Empty;

    public bool HasLink => LinkAddress.Length > 0;

    /// <summary>
    /// Raised when there is a link to hand somewhere. Handing it over is a platform call - the system
    /// share sheet - and reaching for one from here is what would make this untestable.
    /// </summary>
    public event EventHandler<string>? LinkReady;

    /// <summary>
    /// Makes a link if there is not one already, and offers it. Asking first rather than always making
    /// one: a second link would leave the first working, and revoking then only stops one of them.
    /// </summary>
    [RelayCommand]
    private async Task CreateLinkAsync(CancellationToken cancellationToken)
    {
        Message = string.Empty;
        try
        {
            var token = await _links.FindLinkAsync(_kind.ToString(), _itemId, cancellationToken)
                ?? await _links.CreateLinkAsync(_kind.ToString(), _itemId, cancellationToken);

            if (token is null)
            {
                Message = _translations["Couldn't make a link for that."];
                return;
            }

            var webAddress = await _links.WebAddressAsync(cancellationToken);
            if (webAddress.Length == 0)
            {
                // Nothing to build a link around. Saying so beats handing somebody a broken URL.
                Message = _translations["This Orbit doesn't have a web address set, so a link can't be built."];
                return;
            }

            LinkAddress = $"{webAddress.TrimEnd('/')}/s/{token}";
            LinkReady?.Invoke(this, LinkAddress);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["Sharing needs a connection."];
        }
    }

    [RelayCommand]
    private async Task RevokeLinkAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _links.RevokeLinkAsync(_kind.ToString(), _itemId, cancellationToken);
            LinkAddress = string.Empty;
            Message = _translations["That link no longer works."];
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["Sharing needs a connection."];
        }
    }

    public bool CanSend => Recipient is not null && AccessLevel is not null && !IsSharing;

    /// <summary>Points the panel at the thing on screen. Called by the editor as it loads.</summary>
    public void Describes(SharedItemKind kind, Guid itemId, string name)
    {
        _kind = kind;
        _itemId = itemId;
        _name = name;
    }

    [RelayCommand]
    private async Task OpenAsync(CancellationToken cancellationToken)
    {
        Message = string.Empty;

        Recipients.Clear();
        foreach (var contact in await _chatRepository.GetContactsAsync(cancellationToken))
        {
            Recipients.Add(contact);
        }

        if (Recipients.Count == 0)
        {
            Message = _translations["Nobody to share with yet - start a conversation first."];
            return;
        }

        IsOpen = true;
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        Recipient = null;
        Message = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        if (Recipient is not { } recipient || AccessLevel is not { } accessLevel)
        {
            return;
        }

        IsSharing = true;
        try
        {
            var outcome = await _sharing.ShareAsync(
                _kind, _itemId, _name, recipient.UserId, accessLevel.Value.ToString(), cancellationToken);

            Message = outcome switch
            {
                SharingOutcome.Offered => _translations.Format("Shared with {0}.", recipient.DisplayName),
                SharingOutcome.AlreadyShared => _translations["Already shared with that contact - sent a reminder."],
                SharingOutcome.Unreachable => _translations["Sharing needs a connection."],
                _ => _translations["Couldn't share that."]
            };

            if (outcome is SharingOutcome.Offered or SharingOutcome.AlreadyShared)
            {
                IsOpen = false;
                Recipient = null;
            }
        }
        finally
        {
            IsSharing = false;
        }
    }

    private string Describe(ShareAccessLevel level) => level switch
    {
        ShareAccessLevel.ReadOnly => _translations["Read only"],
        ShareAccessLevel.Share => _translations["Can share"],
        _ => _translations["Can edit"]
    };

    partial void OnIsOpenChanged(bool value) => OnPropertyChanged(nameof(IsClosed));

    partial void OnLinkAddressChanged(string value) => OnPropertyChanged(nameof(HasLink));

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnRecipientChanged(LocalContact? value) => SendCommand.NotifyCanExecuteChanged();

    partial void OnIsSharingChanged(bool value) => SendCommand.NotifyCanExecuteChanged();
}
