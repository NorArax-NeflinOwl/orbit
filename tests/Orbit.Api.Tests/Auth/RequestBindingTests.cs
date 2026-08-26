using System.Text.Json;
using Orbit.Contracts.Users;
using Xunit;

namespace Orbit.Api.Tests.Auth;

/// <summary>
/// Covers what a request body missing a required field does. Every request record in Orbit.Contracts is
/// a positional record of non-nullable values, so the binder is what has to refuse one - a field that
/// arrived as null used to reach a handler, get dereferenced, and come back as a 500 the caller could
/// read nothing out of.
///
/// Exercised against the same JsonSerializerOptions Program.cs configures, rather than through HTTP:
/// there is no WebApplicationFactory harness here, and the options are what the behaviour rests on.
/// </summary>
public sealed class RequestBindingTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    [Fact]
    public void A_body_missing_a_required_field_is_refused()
    {
        var body = """{"userName":"alice","displayName":"Alice","password":"s3cret-password"}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RegisterUserRequest>(body, Options));
    }

    [Fact]
    public void A_body_sending_null_for_a_required_field_is_refused()
    {
        // Not covered by RespectRequiredConstructorParameters: the field is there, it is just null.
        var body = """{"email":null,"userName":"alice","displayName":"Alice","password":"s3cret-password"}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RegisterUserRequest>(body, Options));
    }

    [Fact]
    public void A_complete_body_still_binds()
    {
        var body = """{"email":"alice@example.com","userName":"alice","displayName":"Alice","password":"s3cret-password"}""";

        var request = JsonSerializer.Deserialize<RegisterUserRequest>(body, Options);

        Assert.Equal("alice@example.com", request!.Email);
    }

    [Fact]
    public void An_optional_field_may_still_be_left_out()
    {
        // Refusing a missing *required* field must not start refusing a missing optional one - a
        // defaulted parameter is the shape half these contracts use to stay additive.
        var body = """{"name":"Kitchen","items":[]}""";

        var request = JsonSerializer.Deserialize<Orbit.Contracts.Inventory.SaveWarehouseRequest>(body, Options);

        Assert.False(request!.IsPrivate);
        Assert.Null(request.EncryptedContent);
    }
}
