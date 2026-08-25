using Orbit.Api;
using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests;

/// <summary>
/// Covers the one place every endpoint reads an enum a request supplied by name - the step that used to
/// let a missing or misspelled field escape as a 500 instead of a refusal naming what it accepts.
/// </summary>
public sealed class RequestEnumTests
{
    [Theory]
    [InlineData("None", NotificationChannel.None)]
    [InlineData("Email", NotificationChannel.Email)]
    [InlineData("both", NotificationChannel.Both)]
    [InlineData("PUSH", NotificationChannel.Push)]
    public void Parse_reads_a_declared_value_whatever_its_casing(string value, NotificationChannel expected)
        => Assert.Equal(expected, RequestEnum.Parse<NotificationChannel>(value, "channel"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Telepathy")]
    public void Parse_refuses_a_missing_or_unknown_value(string? value)
    {
        var exception = Assert.Throws<InvalidRequestException>(() => RequestEnum.Parse<NotificationChannel>(value, "channel"));

        // The message is what the caller gets back in the 400 body, so it has to name both the field
        // they got wrong and the values that would have worked.
        Assert.Contains("'channel'", exception.Message);
        Assert.Contains("Email", exception.Message);
    }

    [Theory]
    [InlineData("7")]
    [InlineData("99")]
    public void Parse_refuses_a_number_that_is_not_a_declared_value(string value)
    {
        // Enum.TryParse accepts any number for a [Flags] enum, including combinations no switch in the
        // codebase handles - which would then travel all the way into storage before anything noticed.
        Assert.Throws<InvalidRequestException>(() => RequestEnum.Parse<NotificationChannel>(value, "channel"));
    }

    [Fact]
    public void Parse_accepts_a_number_that_names_a_declared_value()
    {
        // 3 is Both (Email | Push), which is declared - refusing it would reject a legitimate value.
        Assert.Equal(NotificationChannel.Both, RequestEnum.Parse<NotificationChannel>("3", "channel"));
    }

    [Theory]
    [InlineData("ReadOnly", ShareAccessLevel.ReadOnly)]
    [InlineData("canedit", ShareAccessLevel.CanEdit)]
    public void Parse_works_for_every_enum_an_endpoint_reads(string value, ShareAccessLevel expected)
        => Assert.Equal(expected, RequestEnum.Parse<ShareAccessLevel>(value, "accessLevel"));
}
