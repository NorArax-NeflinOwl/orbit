using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Mobile;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Update;

namespace Orbit.Mobile.Screens.Update;

/// <summary>
/// Where a newer Orbit comes from - the phone's answer to Orbit.Web's "Get the app" page.
///
/// Two things make it a different screen rather than the same one. It is called Update, because whoever
/// is reading it already has the app; and it shows one platform, its own. The web offers both because
/// it does not know what the reader is holding - a phone does, and a page offering an iPhone build to
/// somebody on Android is a page they have to read past to find their half.
///
/// What is on offer comes from the verdict startup already obtained, so this screen asks nobody: the
/// server names the newer version and where it lives, per platform, and the answer is remembered across
/// launches - see MobileVersionGate.
/// </summary>
public sealed partial class UpdateViewModel : ObservableObject
{
    private readonly MobileVersionGate _versionGate;
    private readonly AppVersion _appVersion;
    private readonly IUpdateLink _updateLink;
    private readonly Translations _translations;

    private string? _updateUrl;

    public UpdateViewModel(
        MobileVersionGate versionGate, AppVersion appVersion, IUpdateLink updateLink, Translations translations)
    {
        _versionGate = versionGate;
        _appVersion = appVersion;
        _updateLink = updateLink;
        _translations = translations;
    }

    /// <summary>Which half of the web's page this is. The other one is not drawn at all.</summary>
    public bool IsAndroid => _appVersion.Platform is MobilePlatform.Android;

    public bool IsIphone => !IsAndroid;

    /// <summary>The build being read on, so "there is a newer one" has something to be newer than.</summary>
    public string InstalledVersion => _appVersion.DisplayVersion;

    /// <summary>
    /// Where the reader stands: a newer build named, or the newest one already installed, or nothing
    /// known because this app has never reached the server. All three are worth saying - a screen that
    /// went blank when it could not check would read as one that had checked and found nothing.
    /// </summary>
    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>
    /// Whether there is somewhere to send the reader. False both when this build is the newest and when
    /// the deployment has published nothing to point at, which is why the summary says which it is.
    /// </summary>
    [ObservableProperty]
    private bool _canUpdate;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var decision = await _versionGate.RememberedDecisionAsync(cancellationToken);
        _updateUrl = decision?.UpdateUrl;
        CanUpdate = _updateUrl is { Length: > 0 };
        Summary = Describe(decision);
    }

    /// <summary>
    /// Leaves Orbit for wherever the build is - a release page, a store listing, a TestFlight
    /// invitation. Opening it is the platform's, which is why it goes through IUpdateLink rather than
    /// being done here.
    /// </summary>
    [RelayCommand]
    private async Task GetItAsync()
    {
        if (_updateUrl is { Length: > 0 } url)
        {
            await _updateLink.OpenAsync(url);
        }
    }

    private string Describe(VersionGateDecision? decision) => decision switch
    {
        // Never reached the server on this build, so nothing has been checked and saying otherwise
        // would be a claim rather than an answer.
        null => _translations["Orbit hasn't been able to check for a newer version yet."],
        { Verdict: MobileVersionVerdict.Supported } => _translations.Format(
            "You have Orbit {0}, which is the newest there is.", InstalledVersion),
        { LatestVersion: { Length: > 0 } latest } => _translations.Format(
            "Orbit {0} is out. You have {1}.", latest, InstalledVersion),
        // A newer build the server did not name, which is worth saying anyway.
        _ => _translations["A newer Orbit is out."]
    };
}
