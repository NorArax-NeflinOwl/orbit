using Microsoft.Extensions.Diagnostics.HealthChecks;
using Orbit.Api.HealthChecks;
using Orbit.Api.Notifications;
using Orbit.Api.Tests.TestDoubles;
using Orbit.GoogleIntegration;
using Xunit;

namespace Orbit.Api.Tests.HealthChecks;

public sealed class ConfigurationHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_every_integration_is_fully_configured()
    {
        var check = CreateHealthCheck(FullSmtpSettings(), FullVapidSettings(), FullGoogleAuthSettings());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_degraded_when_an_integration_is_absent_entirely()
    {
        // A fresh local checkout runs exactly like this - features off is legitimate, so not Unhealthy.
        var check = CreateHealthCheck(new SmtpSettings(), FullVapidSettings(), FullGoogleAuthSettings());

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("email", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_unhealthy_when_an_integration_is_only_half_configured()
    {
        // Host present but no password: SmtpEmailSender would silently drop every email while the
        // deployment looks configured - the one state that is always a mistake.
        var smtpSettings = FullSmtpSettings();
        smtpSettings.Password = null;

        var result = await CreateHealthCheck(smtpSettings, FullVapidSettings(), FullGoogleAuthSettings())
            .CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Smtp:Password", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_reports_a_partial_integration_as_unhealthy_even_when_another_is_absent()
    {
        var vapidSettings = FullVapidSettings();
        vapidSettings.Subject = string.Empty;

        var result = await CreateHealthCheck(new SmtpSettings(), vapidSettings, FullGoogleAuthSettings())
            .CheckHealthAsync(new HealthCheckContext());

        // The mistake (partial push config) must not be masked by the softer "email is off" verdict.
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Vapid:Subject", result.Description);
        Assert.Contains("email", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_names_every_missing_key_in_the_report_data()
    {
        var smtpSettings = FullSmtpSettings();
        smtpSettings.UserName = string.Empty;
        smtpSettings.Password = null;

        var result = await CreateHealthCheck(smtpSettings, FullVapidSettings(), FullGoogleAuthSettings())
            .CheckHealthAsync(new HealthCheckContext());

        // Serialized the same way HealthEndpoints writes the report - the anonymous object's
        // ToString() would not expand the key list.
        var emailReport = System.Text.Json.JsonSerializer.Serialize(result.Data["email"]);
        Assert.Contains("Smtp:UserName", emailReport);
        Assert.Contains("Smtp:Password", emailReport);
        Assert.DoesNotContain("Smtp:Host", emailReport);
    }

    [Fact]
    public async Task CheckHealthAsync_treats_missing_google_sign_in_as_off_rather_than_a_mistake()
    {
        // A single-key integration can never be "partial", so its absence is always just Degraded.
        var result = await CreateHealthCheck(FullSmtpSettings(), FullVapidSettings(), new GoogleAuthSettings())
            .CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("google-sign-in", result.Description);
    }

    private static ConfigurationHealthCheck CreateHealthCheck(
        SmtpSettings smtpSettings, VapidSettings vapidSettings, GoogleAuthSettings googleAuthSettings)
        => new(
            new TestOptionsMonitor<SmtpSettings>(smtpSettings),
            new TestOptionsMonitor<VapidSettings>(vapidSettings),
            new TestOptionsMonitor<GoogleAuthSettings>(googleAuthSettings));

    private static SmtpSettings FullSmtpSettings() => new()
    {
        Host = "smtp.example.test",
        UserName = "orbit@example.test",
        Password = "app-password",
        FromAddress = "orbit@example.test"
    };

    private static VapidSettings FullVapidSettings() => new()
    {
        PublicKeyBase64Url = "public-key",
        PrivateKeyBase64Url = "private-key",
        Subject = "mailto:orbit@example.test"
    };

    private static GoogleAuthSettings FullGoogleAuthSettings() => new()
    {
        ClientId = "000000000000-example.apps.googleusercontent.com"
    };
}
