using Orbit.Core.Abstractions;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// Deterministic <see cref="IVerificationCodeGenerator"/> so a test can know the code that was "emailed"
/// without parsing it back out of the message body. Hashing is a trivial reversible transform rather than
/// a real digest - these tests are about the flow, not about how codes are stored.
/// </summary>
internal sealed class TestVerificationCodeGenerator : IVerificationCodeGenerator
{
    public const string FixedCode = "123456";

    public string Generate() => FixedCode;

    public string Hash(string code) => $"hashed:{code.Trim()}";

    public bool Verify(string code, string codeHash) => Hash(code) == codeHash;
}
