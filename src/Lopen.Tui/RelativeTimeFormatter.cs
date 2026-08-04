namespace Lopen.Tui;

/// <summary>
/// Formats a <see cref="DateTimeOffset"/> as a human-readable relative time string.
/// </summary>
public static class RelativeTimeFormatter
{
    public static string FormatRelativeTime(DateTimeOffset timestamp, DateTimeOffset? relativeTo = null)
    {
        DateTimeOffset now = relativeTo ?? DateTimeOffset.UtcNow;
        TimeSpan elapsed = now - timestamp;

        if (elapsed.TotalSeconds < 0)
        {
            return "just now";
        }

        if (elapsed.TotalMinutes < 1)
        {
            return "just now";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{(int)elapsed.TotalMinutes}m ago";
        }

        if (elapsed.TotalDays < 1)
        {
            return $"{(int)elapsed.TotalHours}h ago";
        }

        if (elapsed.TotalDays < 7)
        {
            return $"{(int)elapsed.TotalDays}d ago";
        }

        if (elapsed.TotalDays < 30)
        {
            return $"{(int)(elapsed.TotalDays / 7)}w ago";
        }

        return $"{(int)(elapsed.TotalDays / 30)}mo ago";
    }
}
