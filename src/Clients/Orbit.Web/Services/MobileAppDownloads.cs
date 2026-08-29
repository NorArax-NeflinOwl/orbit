namespace Orbit.Web.Services;

/// <summary>
/// Where this deployment publishes the phone apps, for the download page to point at.
///
/// Configuration rather than a constant in the page, because where a build is published is a
/// deployment's own business - a GitHub release, blob storage, TestFlight - and no two have to answer
/// the same. Read from wwwroot/appsettings.json the way <c>ApiBaseAddress</c> is, so a deployment sets
/// it without a rebuild of the client.
///
/// Empty is the honest state before anything has been published, and the page says so rather than
/// offering a link that leads nowhere.
/// </summary>
/// <param name="AndroidUrl">The .apk to install, or empty where no Android build has been published.</param>
/// <param name="IosUrl">
/// Where an iPhone is sent - a TestFlight invitation rather than a file, since iOS installs nothing a
/// browser downloaded. Empty until there is one.
/// </param>
public sealed record MobileAppDownloads(string AndroidUrl, string IosUrl)
{
    public bool HasAndroid => AndroidUrl.Length > 0;

    public bool HasIos => IosUrl.Length > 0;
}
