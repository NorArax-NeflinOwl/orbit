namespace Orbit.Web.Services;

/// <summary>
/// Where this deployment's logs and traces can be read - the Aspire dashboard the API sends both to
/// (see docker-compose.yml), or whatever a deployment puts in its place.
///
/// Configuration rather than a constant, for the same reason as <see cref="MobileAppDownloads"/>: where
/// a deployment keeps its logs is its own business, and no two have to answer the same. Read from
/// wwwroot/appsettings.json the way <c>ApiBaseAddress</c> is, so it is set without rebuilding the client.
///
/// Empty is the honest state for a deployment that publishes no dashboard, and the menu then offers
/// nothing rather than a link that leads nowhere.
/// </summary>
public sealed record DiagnosticsDashboard(string Url)
{
    public bool HasUrl => Url.Length > 0;
}
