namespace Orbit.Core.Notifications;

/// <summary>
/// Turns a path Orbit.Api hands out (see SharedItemNotifier.UrlFor) into an address a mail client can
/// open. The web client's own public address is not something Orbit.Core knows on its own - it is
/// wherever this deployment's nginx or `dotnet run` happens to be serving it from - so it is configured
/// once, in Orbit.Api, and reached through here.
/// </summary>
public interface IWebClientLinks
{
    /// <summary>
    /// Null when no public address has been configured for this deployment - a notification then
    /// carries no link rather than a broken one, the same way SmtpEmailSender skips sending outright
    /// rather than failing when nobody has set up email.
    /// </summary>
    string? For(string relativePath);
}
