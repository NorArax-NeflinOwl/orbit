namespace Orbit.Core.Abstractions;

/// <summary>
/// Marks a request record as a client action of the given <see cref="ClientActionCategory"/>, read by
/// <see cref="LoggingDispatcher"/> to tag that request's log lines. Requests without this attribute are
/// logged as before, unchanged.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ClientActionAttribute : Attribute
{
    public ClientActionCategory Category { get; }

    public ClientActionAttribute(ClientActionCategory category)
    {
        Category = category;
    }
}
