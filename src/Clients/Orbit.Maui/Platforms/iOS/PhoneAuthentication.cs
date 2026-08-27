using LocalAuthentication;
using Orbit.Mobile.Security;

namespace Orbit.Maui.Platform;

/// <summary>
/// Asks iOS to confirm the holder with Face ID, Touch ID, or the passcode.
///
/// DeviceOwnerAuthentication rather than the biometrics-only policy: somebody whose face is not
/// recognised in the dark, or who has biometrics switched off, should be able to fall back to the
/// passcode. Refusing them would make the feature something people avoid rather than something that
/// protects them.
/// </summary>
public sealed class PhoneAuthentication : IDeviceAuthentication
{
	public async Task<DeviceAuthenticationOutcome> ConfirmAsync(
		string reason, CancellationToken cancellationToken = default)
	{
		using var context = new LAContext();
		if (!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthentication, out _))
		{
			return DeviceAuthenticationOutcome.NotAvailableOnThisDevice;
		}

		var (confirmed, _) = await context.EvaluatePolicyAsync(LAPolicy.DeviceOwnerAuthentication, reason);
		return confirmed ? DeviceAuthenticationOutcome.Confirmed : DeviceAuthenticationOutcome.Refused;
	}
}
