namespace Orbit.Web.Components;

/// <summary>
/// Turns a display name into avatar initials, and a user id into a stable accent color - shared by
/// every place that renders a contact/user as a small colored circle (Dashboard, Chat, Contacts), so
/// the same person always gets the same initials/color no matter which page is showing them.
/// </summary>
public static class AvatarHelper
{
    /// <summary>
    /// Tolerates a missing name rather than throwing: this is called while building a render tree, so a
    /// null slipping through from an API response would take the whole page down over a decoration.
    /// </summary>
    public static string GetInitials(string? displayName)
    {
        var parts = (displayName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0][..1].ToUpperInvariant(),
            _ => (parts[0][..1] + parts[1][..1]).ToUpperInvariant()
        };
    }

    /// <summary>A stable, deterministic accent color per user id, so the same contact always gets the same avatar color.</summary>
    public static string AvatarColor(Guid userId)
    {
        var hue = Math.Abs(userId.GetHashCode()) % 360;
        return $"oklch(63% 0.13 {hue})";
    }
}
