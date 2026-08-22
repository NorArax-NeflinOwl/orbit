namespace Orbit.Contracts.Tasks;

/// <summary>AccessLevel is "ReadOnly" or "CanEdit" (see Orbit.Core.Abstractions.ShareAccessLevel).</summary>
public sealed record ShareTaskListRequest(Guid RecipientUserId, string AccessLevel = "ReadOnly");
