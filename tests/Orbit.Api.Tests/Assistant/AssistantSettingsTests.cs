using Orbit.Api.Assistant;
using Xunit;

namespace Orbit.Api.Tests.Assistant;

/// <summary>
/// The assistant is optional in every environment, so what counts as "configured" decides whether a
/// fresh checkout starts at all - the same question SmtpSettings and VapidSettings answer for email and
/// push.
/// </summary>
public sealed class AssistantSettingsTests
{
    [Fact]
    public void A_checkout_that_configured_nothing_has_no_assistant()
    {
        Assert.False(new AssistantSettings().IsConfigured);
    }

    [Fact]
    public void An_endpoint_without_a_model_is_not_enough_to_ask_anything()
    {
        var settings = new AssistantSettings { Endpoint = "http://ollama:11434/v1" };

        Assert.False(settings.IsConfigured);
    }

    [Fact]
    public void A_model_name_without_anywhere_to_send_it_is_not_enough_either()
    {
        var settings = new AssistantSettings { Model = "llama3.2:3b" };

        Assert.False(settings.IsConfigured);
    }

    [Fact]
    public void A_local_model_needs_no_key()
    {
        // Ollama authenticates nobody, so requiring a key here would make the local setup - the one the
        // whole assistant is developed against - impossible to configure.
        var settings = new AssistantSettings { Endpoint = "http://ollama:11434/v1", Model = "llama3.2:3b" };

        Assert.True(settings.IsConfigured);
    }

    [Fact]
    public void Whitespace_is_not_configuration()
    {
        var settings = new AssistantSettings { Endpoint = "   ", Model = "   " };

        Assert.False(settings.IsConfigured);
    }
}
