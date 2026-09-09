using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core;
using Orbit.Core.Permissions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Permissions;

namespace Orbit.Mobile.Screens.About;

/// <summary>
/// What Orbit is, which build this one is, and where to read the documents about it.
///
/// A screen rather than the fold-out it used to be at the foot of the drawer. The drawer's other
/// entries all take the reader somewhere and this one did not, and three lines squeezed under a menu
/// left no room for the row of documents the web's footer has carried all along.
///
/// Nothing here can be acted on: it is the one screen in Orbit that is only writing, which is why it
/// carries no menu of its own.
/// </summary>
public sealed partial class AboutViewModel : ObservableObject
{
    private readonly Translations _translations;
    private readonly ServerVersionClient _serverVersion;
    private readonly UserPermissions _permissions;

    /// <summary>
    /// This build, read off this assembly rather than off Orbit.Core's - the number is per client, and
    /// the shared project is compiled into three of them. See OrbitVersion, and the drawer's own
    /// version line, which reads the same one.
    /// </summary>
    private static readonly OrbitVersion Build = OrbitVersion.ReadFrom(typeof(AboutViewModel).Assembly);

    public AboutViewModel(
        Translations translations, ServerVersionClient serverVersion, UserPermissions permissions,
        OrbitDocumentLinks documents)
    {
        _translations = translations;
        _serverVersion = serverVersion;
        _permissions = permissions;

        Documents = [.. Rows(documents, translations)];
    }

    /// <summary>What Orbit is, in one line - the same sentence the web's About dialog opens with.</summary>
    public string Description =>
        _translations["Orbit keeps notes, task lists, a calendar and what is on your shelves, and lets you talk and share - in one place."];

    public string Copyright => OrbitRelease.Copyright;

    /// <summary>
    /// What this build says about itself. The commit is detail about Orbit's own inside, so it goes to
    /// the accounts holding Debug and to nobody else - the rule both other ends already apply.
    /// </summary>
    private OrbitVersion Shown => _permissions.Has(ApplicationPermission.Debug)
        ? Build
        : Build.WithoutTheCommit();

    public string Version => IsWholeCommitShown ? Shown.Full : Shown.Short;

    /// <summary>Whether the row is showing the whole commit hash rather than the first seven of it.</summary>
    [ObservableProperty]
    private bool _isWholeCommitShown;

    /// <summary>
    /// Whether tapping the version does anything. False for a build carrying no commit, and for a
    /// reader who is not shown one - the row would otherwise look pressable and do nothing.
    /// </summary>
    public bool CanShowTheWholeCommit => Shown.CanShowTheWholeCommit;

    /// <summary>
    /// Tapping the version grows the rest of the hash while debugging. The short form is what anybody
    /// reads; the whole one is what a `git checkout` takes.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanShowTheWholeCommit))]
    private void ShowTheWholeCommit()
    {
        IsWholeCommitShown = !IsWholeCommitShown;
        OnPropertyChanged(nameof(Version));
    }

    /// <summary>
    /// Which build of the server this app is talking to, once it has been asked. Empty until then and
    /// when it cannot be reached - an offline screen should say nothing about the server rather than
    /// guess, and the app's own version is still worth showing on its own.
    /// </summary>
    [ObservableProperty]
    private string _serverBuild = string.Empty;

    public bool HasServerBuild => ServerBuild.Length > 0;

    /// <summary>The documents this deployment publishes about itself, in the web footer's own order.</summary>
    public ObservableCollection<AboutDocument> Documents { get; }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        OnPropertyChanged(nameof(Version));
        OnPropertyChanged(nameof(CanShowTheWholeCommit));
        ShowTheWholeCommitCommand.NotifyCanExecuteChanged();

        if (HasServerBuild || await _serverVersion.GetAsync(cancellationToken) is not { } server)
        {
            return;
        }

        ServerBuild = server.CommitHash.Length == 0
            ? $"api ver:{server.Version}"
            : $"api ver:{server.Version}+gitHash:{Shorten(server.CommitHash)}";
        OnPropertyChanged(nameof(HasServerBuild));
    }

    private static string Shorten(string commitHash) => commitHash.Length > 7 ? commitHash[..7] : commitHash;

    partial void OnServerBuildChanged(string value) => OnPropertyChanged(nameof(HasServerBuild));

    /// <summary>
    /// The five rows, minus any this build has no address for. Left out rather than shown and dead: a
    /// row that opens nothing teaches the reader that the list does not work.
    /// </summary>
    private static IEnumerable<AboutDocument> Rows(OrbitDocumentLinks documents, Translations translations)
    {
        if (documents.Privacy is { } privacy)
        {
            yield return new AboutDocument(translations["Privacy"], privacy);
        }

        if (documents.Security is { } security)
        {
            yield return new AboutDocument(translations["Security"], security);
        }

        if (documents.Documentation is { } docs)
        {
            yield return new AboutDocument(translations["Docs"], docs);
        }

        if (documents.DoNotShare is { } doNotShare)
        {
            yield return new AboutDocument(translations["Do not share my personal information"], doNotShare);
        }

        yield return new AboutDocument(translations[OrbitRelease.LicenseName], OrbitRelease.LicenseUrl);
    }
}

/// <summary>
/// One row of the About screen's list: what the document is called, and where it is. Opened outside
/// the app, which is what these are - pages of the web client and a file in the repository, neither of
/// which the phone has a copy of.
/// </summary>
public sealed record AboutDocument(string Name, string Url);
