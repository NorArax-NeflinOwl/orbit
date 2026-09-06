namespace Orbit.Api.RateLimiting;

/// <summary>
/// A bucket shared by every caller a policy could not tell apart, spent alongside their own.
///
/// It answers a question the per-caller limit cannot: an anonymous caller is partitioned by the address
/// the request appears to come from, and that address is only as honest as the proxy chain that
/// produced it. Where a forwarded header can be forged, each request lands in a partition of its own and
/// the per-caller budget stops bounding anything at all.
///
/// So it is a floor under the worst case rather than a limit anybody should meet. It is set well above
/// honest traffic on purpose, because it is also the one thing an attacker can deliberately exhaust to
/// make everybody else wait - and a ceiling low enough to be a good brute-force bound would be a good
/// denial of service too. That trade is the reason it is generous, not an oversight.
/// </summary>
/// <param name="Partition">Shared by every caller under the policy, so it must not include the caller.</param>
public sealed record RateLimitCeiling(string Partition, int PermitLimit);
