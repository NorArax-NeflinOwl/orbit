using Orbit.Core.Abstractions;

namespace Orbit.Api;

/// <summary>
/// Reads an enum value that a request supplied by name, in the one place every endpoint can share.
/// <see cref="Enum.Parse{TEnum}(string, bool)"/> on its own is wrong for request data twice over: a
/// missing field reaches it as null and comes back as a null-argument fault, and an unknown name comes
/// back worded for a programmer ("Requested value 'X' was not found") rather than for whoever sent it.
/// Both become a refusal that names the field and what it accepts.
/// </summary>
internal static class RequestEnum
{
    public static TEnum Parse<TEnum>(string? value, string fieldName) where TEnum : struct, Enum
    {
        // IsDefined as well as TryParse: TryParse also accepts numbers ("7") and undeclared flag
        // combinations, which would otherwise slip through as values no switch in the codebase handles.
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new InvalidRequestException(
            $"'{fieldName}' must be one of: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }
}
