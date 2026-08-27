using AndroidX.Biometric;
using AndroidX.Fragment.App;
using Java.Lang;
using Orbit.Mobile.Security;

namespace Orbit.Maui.Platform;

/// <summary>
/// Asks Android to confirm the holder with a fingerprint, a face, or the screen lock.
///
/// A weak biometric together with the device credential, rather than a strong biometric alone, so this
/// answers the same question iOS's DeviceOwnerAuthentication does: somebody whose finger is not read in
/// the cold, or who has no biometrics enrolled at all, falls back to their PIN, pattern or password
/// instead of being shut out. Refusing them would make the feature something people switch off rather
/// than something that protects them. Nothing here unwraps a key - that is what a strong biometric
/// would be needed for, and Orbit's chat key is deliberately not held that way (see SecureChatKeyStorage).
/// </summary>
public sealed class PhoneAuthentication : IDeviceAuthentication
{
	private const int AcceptedAuthenticators =
		BiometricManager.Authenticators.BiometricWeak | BiometricManager.Authenticators.DeviceCredential;

	/// <summary>
	/// On the main thread throughout: the prompt is a fragment, and Android attaches it to the activity
	/// that is in front of the reader. A callable arriving on a background thread - which is where an
	/// awaited HTTP call can leave a caller - would throw from inside the fragment manager.
	/// </summary>
	public Task<DeviceAuthenticationOutcome> ConfirmAsync(
		string reason, CancellationToken cancellationToken = default)
		=> MainThread.InvokeOnMainThreadAsync(() => ShowPromptAsync(reason, cancellationToken));

	private static async Task<DeviceAuthenticationOutcome> ShowPromptAsync(
		string reason, CancellationToken cancellationToken)
	{
		if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not FragmentActivity activity)
		{
			return DeviceAuthenticationOutcome.NotAvailableOnThisDevice;
		}

		if (BiometricManager.From(activity).CanAuthenticate(AcceptedAuthenticators)
			!= BiometricManager.BiometricSuccess)
		{
			return DeviceAuthenticationOutcome.NotAvailableOnThisDevice;
		}

		// Continuations asynchronously: the answer arrives on the executor Android runs the prompt on,
		// and resuming the caller inside that callback would run the rest of their work there.
		var answer = new TaskCompletionSource<DeviceAuthenticationOutcome>(
			TaskCreationOptions.RunContinuationsAsynchronously);
		var prompt = new BiometricPrompt(activity, new PromptAnswer(answer));

		using (cancellationToken.Register(() => MainThread.BeginInvokeOnMainThread(prompt.CancelAuthentication)))
		{
			prompt.Authenticate(DescribeWhatIsBeingOpened(reason));
			return await answer.Task;
		}
	}

	/// <summary>
	/// The title is what Android shows largest and the subtitle sits under it, so the reason - which
	/// says what is about to be revealed - goes in the subtitle rather than the heading.
	///
	/// No negative button, deliberately: Android rejects a prompt carrying one when the device
	/// credential is an accepted authenticator, because the screen lock is then the way out.
	/// </summary>
	private static BiometricPrompt.PromptInfo DescribeWhatIsBeingOpened(string reason)
		=> new BiometricPrompt.PromptInfo.Builder()
			.SetTitle("Orbit")!
			.SetSubtitle(reason)!
			.SetAllowedAuthenticators(AcceptedAuthenticators)!
			.Build();

	/// <summary>
	/// Android reports three things and only two of them end the prompt. A finger that did not match
	/// arrives as OnAuthenticationFailed with the prompt still up and the reader still trying, so it is
	/// deliberately not handled here - answering there would answer a question still being asked.
	/// </summary>
	private sealed class PromptAnswer : BiometricPrompt.AuthenticationCallback
	{
		private readonly TaskCompletionSource<DeviceAuthenticationOutcome> _answer;

		public PromptAnswer(TaskCompletionSource<DeviceAuthenticationOutcome> answer) => _answer = answer;

		public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
			=> _answer.TrySetResult(DeviceAuthenticationOutcome.Confirmed);

		public override void OnAuthenticationError(int errorCode, ICharSequence errorMessage)
			=> _answer.TrySetResult(OutcomeFor(errorCode));

		/// <summary>
		/// Cancelling and running out of attempts are both refusals: the reader did not get in, and can
		/// try again later. Having nothing to ask with is not, and CanAuthenticate normally catches it
		/// before the prompt is ever shown - this covers the screen lock being removed in between.
		/// </summary>
		private static DeviceAuthenticationOutcome OutcomeFor(int errorCode)
			=> errorCode is BiometricPrompt.ErrorNoDeviceCredential or BiometricPrompt.ErrorHwNotPresent
				or BiometricPrompt.ErrorNoBiometrics or BiometricPrompt.ErrorSecurityUpdateRequired
				? DeviceAuthenticationOutcome.NotAvailableOnThisDevice
				: DeviceAuthenticationOutcome.Refused;
	}
}
