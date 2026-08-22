namespace Orbit.Contracts.Chat;

public sealed record EditMessageRequest(string CiphertextBase64, string NonceBase64);
