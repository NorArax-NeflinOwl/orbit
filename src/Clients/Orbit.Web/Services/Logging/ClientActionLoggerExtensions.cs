using Orbit.Core.Abstractions;

namespace Orbit.Web.Services.Logging;

/// <summary>
/// Client-side counterpart to Orbit.Api's LoggingDispatcher: tags log lines for actions a person
/// explicitly triggered (login, save, share, ...) with the same "[ACTION:{Category}]" prefix, using the
/// same ClientActionCategory enum the server tags its own log lines with, so client and server logs read
/// the same way when scanned or grepped together.
/// </summary>
public static class ClientActionLoggerExtensions
{
    public static void LogActionCompleted(this ILogger logger, ClientActionCategory category, string actionName)
        => logger.LogInformation("[ACTION:{ActionCategory}] {ActionName} completed", category, actionName);

    public static void LogActionFailed(this ILogger logger, ClientActionCategory category, string actionName, Exception exception)
        => logger.LogError(exception, "[ACTION:{ActionCategory}] {ActionName} failed", category, actionName);
}
