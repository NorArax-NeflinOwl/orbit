using Microsoft.Extensions.Configuration;
using Orbit.Api.HealthChecks;
using Xunit;

namespace Orbit.Api.Tests.HealthChecks;

/// <summary>
/// Confirms the shape of the "HealthChecks" configuration section actually matches
/// <see cref="HealthCheckSettings"/>, so a renamed property here can't silently stop binding while
/// appsettings.json keeps the old key.
/// </summary>
public sealed class HealthCheckSettingsBindingTests
{
    [Fact]
    public void Configuration_section_binds_to_HealthCheckSettings()
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["HealthChecks:Database:Enabled"] = "false",
            ["HealthChecks:DiskSpace:Enabled"] = "true",
            ["HealthChecks:DiskSpace:MinimumFreeBytes"] = "123456789",
            ["HealthChecks:ExternalServices:Enabled"] = "true",
            ["HealthChecks:ExternalServices:Services:0:Name"] = "push-notifications",
            ["HealthChecks:ExternalServices:Services:0:Url"] = "https://push.example.test/health",
            ["HealthChecks:ExternalServices:Services:0:Enabled"] = "true",
            ["HealthChecks:ExternalServices:Services:0:TimeoutMs"] = "2500",
            ["HealthChecks:HostedServices:Enabled"] = "true",
            ["HealthChecks:HostedServices:StaleAfterSeconds"] = "60"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();

        var settings = configuration.GetSection("HealthChecks").Get<HealthCheckSettings>();

        Assert.NotNull(settings);
        Assert.False(settings.Database.Enabled);
        Assert.Equal(123456789, settings.DiskSpace.MinimumFreeBytes);
        var externalService = Assert.Single(settings.ExternalServices.Services);
        Assert.Equal("push-notifications", externalService.Name);
        Assert.Equal(2500, externalService.TimeoutMs);
        Assert.Equal(60, settings.HostedServices.StaleAfterSeconds);
    }

    [Fact]
    public void Missing_section_binds_to_defaults()
    {
        var configuration = new ConfigurationBuilder().Build();

        var settings = configuration.GetSection("HealthChecks").Get<HealthCheckSettings>() ?? new HealthCheckSettings();

        Assert.True(settings.Database.Enabled);
        Assert.True(settings.DiskSpace.Enabled);
        Assert.Empty(settings.ExternalServices.Services);
    }
}
