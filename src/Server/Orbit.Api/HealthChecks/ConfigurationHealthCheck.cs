using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Orbit.Api.Notifications;
using Orbit.GoogleIntegration;

namespace Orbit.Api.HealthChecks;

/// <summary>
/// Reports whether each optional integration (SMTP email, VAPID push, Google sign-in) is actually
/// configured. The senders behind these features deliberately degrade to a log line when their
/// settings are missing - see <see cref="SmtpEmailSender"/> - so a configuration gap is invisible
/// from inside the app; this check is what makes it visible on /health. A fully absent integration
/// is Degraded (turning a feature off can be a deliberate choice); a partially configured one is
/// Unhealthy, because half a configuration is never intentional - it means email or push silently
/// drops everything while looking set up.
/// </summary>
public sealed class ConfigurationHealthCheck : IHealthCheck
{
    private readonly IOptionsMonitor<SmtpSettings> _smtpSettings;
    private readonly IOptionsMonitor<VapidSettings> _vapidSettings;
    private readonly IOptionsMonitor<GoogleAuthSettings> _googleAuthSettings;

    public ConfigurationHealthCheck(
        IOptionsMonitor<SmtpSettings> smtpSettings,
        IOptionsMonitor<VapidSettings> vapidSettings,
        IOptionsMonitor<GoogleAuthSettings> googleAuthSettings)
    {
        _smtpSettings = smtpSettings;
        _vapidSettings = vapidSettings;
        _googleAuthSettings = googleAuthSettings;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var integrations = EvaluateIntegrations();

        var data = integrations.ToDictionary(
            integration => integration.Name,
            integration => (object)new { status = integration.State.ToString(), missingKeys = integration.MissingKeys });

        if (integrations.Any(integration => integration.State == IntegrationState.PartiallyConfigured))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(Describe(integrations), data: data));
        }

        if (integrations.Any(integration => integration.State == IntegrationState.NotConfigured))
        {
            return Task.FromResult(HealthCheckResult.Degraded(Describe(integrations), data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Every integration is fully configured.", data));
    }

    /// <summary>
    /// Keys are listed under their configuration paths (colon-separated; "__" in an environment
    /// variable) so the report names exactly what an operator has to set. Smtp:Port and
    /// Smtp:FromDisplayName are absent on purpose: they have working defaults, so they can't be
    /// "missing".
    /// </summary>
    private IReadOnlyList<IntegrationConfiguration> EvaluateIntegrations()
    {
        var smtp = _smtpSettings.CurrentValue;
        var vapid = _vapidSettings.CurrentValue;
        var googleAuth = _googleAuthSettings.CurrentValue;

        return
        [
            IntegrationConfiguration.Evaluate("email", new Dictionary<string, string?>
            {
                ["Smtp:Host"] = smtp.Host,
                ["Smtp:UserName"] = smtp.UserName,
                ["Smtp:Password"] = smtp.Password,
                ["Smtp:FromAddress"] = smtp.FromAddress
            }),
            IntegrationConfiguration.Evaluate("push-notifications", new Dictionary<string, string?>
            {
                ["Vapid:PublicKeyBase64Url"] = vapid.PublicKeyBase64Url,
                ["Vapid:PrivateKeyBase64Url"] = vapid.PrivateKeyBase64Url,
                ["Vapid:Subject"] = vapid.Subject
            }),
            IntegrationConfiguration.Evaluate("google-sign-in", new Dictionary<string, string?>
            {
                ["GoogleAuth:ClientId"] = googleAuth.ClientId
            })
        ];
    }

    private static string Describe(IReadOnlyList<IntegrationConfiguration> integrations)
    {
        var sentences = new List<string>();

        var partial = integrations.Where(integration => integration.State == IntegrationState.PartiallyConfigured).ToList();
        if (partial.Count > 0)
        {
            var details = partial.Select(integration => $"{integration.Name} (missing {string.Join(", ", integration.MissingKeys)})");
            sentences.Add($"Partially configured - set the missing keys or remove the leftover ones: {string.Join("; ", details)}.");
        }

        var absent = integrations.Where(integration => integration.State == IntegrationState.NotConfigured).ToList();
        if (absent.Count > 0)
        {
            sentences.Add($"Not configured, so the feature is off: {string.Join(", ", absent.Select(integration => integration.Name))}.");
        }

        return string.Join(" ", sentences);
    }

    private enum IntegrationState
    {
        Configured,
        NotConfigured,
        PartiallyConfigured
    }

    /// <summary>One integration's verdict: which of its keys are missing and what that means overall.</summary>
    private sealed record IntegrationConfiguration(string Name, IntegrationState State, IReadOnlyList<string> MissingKeys)
    {
        public static IntegrationConfiguration Evaluate(string name, IReadOnlyDictionary<string, string?> keys)
        {
            var missingKeys = keys
                .Where(key => string.IsNullOrWhiteSpace(key.Value))
                .Select(key => key.Key)
                .ToList();

            var state = missingKeys.Count == 0
                ? IntegrationState.Configured
                : missingKeys.Count == keys.Count
                    ? IntegrationState.NotConfigured
                    : IntegrationState.PartiallyConfigured;

            return new IntegrationConfiguration(name, state, missingKeys);
        }
    }
}
