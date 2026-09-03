using Orbit.Core.Notifications;

namespace Orbit.Api.Notifications;

/// <summary>
/// Reads the web client's own public address from configuration - see IWebClientLinks. Bound from the
/// flat "WebClientBaseUrl" setting rather than a section, the same way WebClientOrigins is: both name
/// the same web client, just for different purposes (CORS there, a mailable link here).
/// </summary>
public sealed class WebClientLinks : IWebClientLinks
{
    private readonly string? _baseUrl;

    public WebClientLinks(IConfiguration configuration)
        => _baseUrl = configuration["WebClientBaseUrl"];

    public string? For(string relativePath)
        => string.IsNullOrWhiteSpace(_baseUrl) ? null : $"{_baseUrl.TrimEnd('/')}{relativePath}";
}
