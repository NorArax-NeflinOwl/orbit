using System.Text.Json.Serialization;
using Orbit.Mobile.Authentication;

namespace Orbit.Maui.Platform;

/// <summary>
/// Source-generated serialization for the stored session. Release builds trim and AOT-compile the app,
/// which strips the reflection that <see cref="System.Text.Json"/> would otherwise need - a session that
/// round-trips in Debug and silently fails to in the shipped build is exactly the kind of difference
/// that is found late.
/// </summary>
[JsonSerializable(typeof(UserSession))]
internal sealed partial class SessionSerializerContext : JsonSerializerContext;
