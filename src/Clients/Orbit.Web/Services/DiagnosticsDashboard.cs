namespace Orbit.Web.Services;

/// <summary>
/// Where this deployment's logs can be read. Two addresses, because the question has two answers and
/// they are not interchangeable:
///
/// - <see cref="HistoryUrl"/> is what was logged - yesterday's failure, last week's flood. On Azure that
///   is the Application Insights resource the API sends its traces and its log lines to; locally it is
///   the Aspire dashboard's structured logs.
/// - <see cref="LiveUrl"/> is what is happening this second, and keeps nothing. On Azure that is the
///   container's own log stream; locally the Aspire dashboard's console.
///
/// Configuration rather than constants, for the same reason as <see cref="MobileAppDownloads"/>: where a
/// deployment keeps its logs is its own business, and no two have to answer the same. Read from
/// wwwroot/appsettings.json the way <c>ApiBaseAddress</c> is, so they are set without rebuilding the
/// client - see write-diagnostics-dashboard.sh, which writes them in when the container starts.
///
/// Empty is the honest state for a deployment that publishes neither, and the menu then offers nothing
/// rather than a link that leads nowhere.
/// </summary>
public sealed record DiagnosticsDashboard(string HistoryUrl, string LiveUrl)
{
    public bool HasHistory => HistoryUrl.Length > 0;

    public bool HasLive => LiveUrl.Length > 0;

    /// <summary>Whether there is anything to offer at all.</summary>
    public bool HasAny => HasHistory || HasLive;

    /// <summary>
    /// Whether the reader has an actual choice to make. With one of the two, asking which they want
    /// would be a question with one answer, so the entry opens it directly.
    /// </summary>
    public bool HasBoth => HasHistory && HasLive;

    /// <summary>The one there is, when there is only one - see <see cref="HasBoth"/>.</summary>
    public string TheOnlyUrl => HasHistory ? HistoryUrl : LiveUrl;
}
