using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Diagnostics;
using Orbit.Mobile.Api;
using Orbit.Mobile.Diagnostics;
using Orbit.Mobile.Update;

namespace Orbit.Mobile.Screens.Diagnostics;

/// <summary>
/// The app's own log, and the one way it leaves the phone.
///
/// Sending is explicit and user-initiated, never automatic - the plan's §8 makes that a rule, and this
/// screen is the whole of it: the reader looks at what would be sent, and then decides. The counterpart
/// of Orbit.Web's "Show exceptions" switch on its Options page, with an upload instead of a clipboard.
/// </summary>
public sealed partial class DiagnosticsViewModel : ObservableObject
{
    /// <summary>
    /// How much of the log the screen shows. Enough to see what is being sent without rendering a
    /// quarter of a megabyte into a phone's layout.
    /// </summary>
    private const int PreviewLines = 60;

    private readonly DiagnosticLogFile _log;
    private readonly DiagnosticLogVerbosity _verbosity;
    private readonly DiagnosticsClient _diagnosticsClient;
    private readonly IDeviceDescription _device;
    private readonly AppVersion _appVersion;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _preview = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public DiagnosticsViewModel(
        DiagnosticLogFile log, DiagnosticLogVerbosity verbosity, DiagnosticsClient diagnosticsClient,
        IDeviceDescription device, AppVersion appVersion, IScreenNavigator navigator)
    {
        _log = log;
        _verbosity = verbosity;
        _diagnosticsClient = diagnosticsClient;
        _device = device;
        _appVersion = appVersion;
        _navigator = navigator;
    }

    public bool HasMessage => Message.Length > 0;

    public bool HasNothing => Preview.Length == 0;

    /// <summary>
    /// Whether everything is being written down rather than only warnings and worse. Not remembered
    /// across launches on purpose - see DiagnosticLogVerbosity.
    /// </summary>
    public bool IsVerbose
    {
        get => _verbosity.IsVerbose;
        set
        {
            _verbosity.IsVerbose = value;
            OnPropertyChanged();
        }
    }

    /// <summary>What the upload will say this device is, shown so nothing about it is a surprise.</summary>
    public string DeviceSummary
        => $"{_appVersion.Platform} {_device.OperatingSystemVersion} · {_device.Model} · Orbit {_appVersion.DisplayVersion}";

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnPreviewChanged(string value) => OnPropertyChanged(nameof(HasNothing));

    [RelayCommand]
    private void Load() => ShowTail();

    [RelayCommand]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        var contents = _log.ReadAll();
        if (contents.Length == 0)
        {
            Message = "There is nothing to send yet.";
            return;
        }

        IsBusy = true;
        try
        {
            var stored = await _diagnosticsClient.UploadAsync(
                new UploadDiagnosticLogRequest(
                    contents, _appVersion.DisplayVersion, _appVersion.Platform.ToString(),
                    _device.OperatingSystemVersion, _device.Model),
                cancellationToken);

            // Zero readable entries is worth saying rather than claiming success: the log arrived and
            // told the server nothing, which is a different thing from having been sent.
            Message = stored > 0
                ? $"Sent {stored} entries. Thank you."
                : "Sent, but nothing in the log could be read.";
        }
        catch (HttpRequestException exception)
        {
            Message = exception.StatusCode is null
                ? "Couldn't send it - Orbit is out of reach."
                : "Orbit wouldn't accept the log. Try signing in again.";
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Offered because the log is the reader's own record of their own device: somebody who has decided
    /// not to send it should be able to be rid of it.
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        _log.Clear();
        ShowTail();
        Message = "Cleared.";
    }

    [RelayCommand]
    private void GoBack() => _navigator.ShowAccount();

    /// <summary>
    /// The end of the log rather than the start. A log is read backwards from whatever just went wrong,
    /// so the last lines are the ones worth putting on screen.
    /// </summary>
    private void ShowTail()
    {
        var lines = _log.ReadAll().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Preview = string.Join('\n', lines.TakeLast(PreviewLines));
    }
}
