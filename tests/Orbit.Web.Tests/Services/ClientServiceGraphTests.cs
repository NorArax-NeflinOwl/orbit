using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Orbit.Web.Services;
using Orbit.Web.Services.Logging;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Guards the shape of the client's service graph. Nothing here tests behaviour - it tests that the
/// container can be built and its services actually resolved, which is the failure that takes the whole
/// app down at startup with nothing rendered but "Loading…".
///
/// Written after exactly that: PersistentLoggerProvider was given DevicePreferences, DevicePreferences
/// took an ILogger, and the resulting cycle (ILoggerProvider -> DevicePreferences -> ILogger&lt;T&gt; ->
/// ILoggerFactory -> ILoggerProvider) reached production. Every unit test passed, because a unit test
/// constructs its subject directly and never asks the container for anything.
/// </summary>
public sealed class ClientServiceGraphTests
{
    [Fact]
    public void The_logging_pipeline_resolves()
    {
        using var provider = BuildContainer();

        // Resolving any ILogger builds every registered ILoggerProvider. A cycle anywhere in that graph
        // throws here rather than at startup in a browser.
        var logger = provider.GetRequiredService<ILogger<ClientServiceGraphTests>>();

        Assert.NotNull(logger);
    }

    [Fact]
    public void Nothing_the_logger_depends_on_depends_on_the_logger()
    {
        using var provider = BuildContainer();

        // The specific cycle that broke production. DevicePreferences must stay constructible without
        // the logging pipeline existing at all - see its class comment.
        var preferences = provider.GetRequiredService<DevicePreferences>();

        Assert.Equal(LogLevel.Warning, preferences.MinimumLogLevel);
    }

    [Fact]
    public void Every_logger_provider_can_be_created()
    {
        using var provider = BuildContainer();

        var loggerProviders = provider.GetServices<ILoggerProvider>().ToList();

        Assert.NotEmpty(loggerProviders);
        Assert.All(loggerProviders, loggerProvider => Assert.NotNull(loggerProvider.CreateLogger("Test")));
    }

    [Fact]
    public void A_cycle_is_what_this_actually_catches()
    {
        // Proves the mechanism rather than trusting it: without ValidateOnBuild these registrations
        // build happily and only throw on the first resolve - which in a browser is startup, with
        // nothing on screen but "Loading…".
        var services = new ServiceCollection();
        services.AddSingleton<FirstLink>();
        services.AddSingleton<SecondLink>();

        Assert.Throws<AggregateException>(() => services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        }));
    }

    private sealed class FirstLink(SecondLink second)
    {
        public SecondLink Second { get; } = second;
    }

    private sealed class SecondLink(FirstLink first)
    {
        public FirstLink First { get; } = first;
    }

    /// <summary>
    /// The client's own registrations, minus the HttpClients - those need a base address that only
    /// Program.cs knows, and none of them take part in the cycle this guards against.
    /// </summary>
    private static ServiceProvider BuildContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new StubJSRuntime());
        services.AddLogging();
        services.AddSingleton<ILoggerProvider, PersistentLoggerProvider>();
        services.AddSingleton<DevicePreferences>();
        services.AddSingleton<Translations>();
        services.AddScoped<ThemeService>();

        // validateScopes/validateOnBuild are what turn a bad graph into a failure here rather than on
        // the first resolve in a browser.
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }
}
