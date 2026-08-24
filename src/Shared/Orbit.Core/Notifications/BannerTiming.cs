namespace Orbit.Core.Notifications;

/// <summary>
/// How the in-app toast banner behaves for one user: how long a single banner stays on screen, and the
/// minimum quiet gap before the next one may appear. Grouped as a value object because the two always
/// travel together (settings row, Options form, MainLayout's banner loop) and are meaningless apart.
/// Out-of-range input is clamped rather than rejected - a settings form should never hard-fail over a
/// typo in a number field.
/// </summary>
public sealed record BannerTiming
{
    public const int MinimumSeconds = 1;
    public const int MaximumVisibleSeconds = 30;
    public const int MaximumGapSeconds = 300;

    public int VisibleSeconds { get; }
    public int MinimumGapSeconds { get; }

    public BannerTiming(int visibleSeconds, int minimumGapSeconds)
    {
        VisibleSeconds = Math.Clamp(visibleSeconds, MinimumSeconds, MaximumVisibleSeconds);
        MinimumGapSeconds = Math.Clamp(minimumGapSeconds, MinimumSeconds, MaximumGapSeconds);
    }

    public static BannerTiming Default => new(visibleSeconds: 5, minimumGapSeconds: 5);
}
