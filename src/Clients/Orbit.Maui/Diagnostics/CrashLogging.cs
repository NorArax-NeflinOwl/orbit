using Orbit.Mobile.Diagnostics;

namespace Orbit.Maui.Diagnostics;

/// <summary>
/// Puts every way an exception can go unhandled into the log the reader can send - see
/// <see cref="CrashLog"/> for why a crash was the one failure nothing recorded.
///
/// Three hooks rather than one, because they do not overlap. On Android an exception thrown on the UI
/// thread reaches the runtime through the Java side, and that path fires
/// <c>AndroidEnvironment.UnhandledExceptionRaiser</c> and nothing else - which is exactly the path a
/// failing RelayCommand takes, so it is the one that mattered most and the one a lone AppDomain handler
/// would have missed.
/// </summary>
public static class CrashLogging
{
	/// <summary>
	/// Subscribed once, for the life of the process, and never unsubscribed: there is no later at which
	/// the app would want a crash to go unrecorded.
	/// </summary>
	public static void Watch(CrashLog crashLog)
	{
		AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) => crashLog.Record(
			eventArgs.ExceptionObject as Exception, "the runtime", eventArgs.IsTerminating);

		// Raised when a failed task is collected with nobody having awaited it. Marked observed
		// afterwards so noticing it does not itself become the thing that ends the process.
		TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
		{
			crashLog.Record(eventArgs.Exception, "an unwatched task", isTerminating: false);
			eventArgs.SetObserved();
		};

#if ANDROID
		Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (_, eventArgs) => crashLog.Record(
			eventArgs.Exception, "Android", eventArgs.Handled is false);
#endif
	}
}
