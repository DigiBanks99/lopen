using System.Globalization;
using System.Text.RegularExpressions;

namespace Lopen.Storage;

/// <summary>
/// Represents a unique session identifier in the format {module}-YYYYMMDD-{counter}.
/// </summary>
public sealed partial class SessionId : IEquatable<SessionId>
{
    /// <summary>The module name (e.g., "auth", "core").</summary>
    public string Module { get; }

    /// <summary>The date the session was created.</summary>
    public DateOnly Date { get; }

    /// <summary>The monotonically increasing counter for the module on this date.</summary>
    public int Counter { get; }

    private SessionId(string module, DateOnly date, int counter)
    {
        Module = module;
        Date = date;
        Counter = counter;
    }

    /// <summary>
    /// Generates a new session ID for the given module and date with the specified counter.
    /// </summary>
    public static SessionId Generate(string module, DateOnly date, int counter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentOutOfRangeException.ThrowIfLessThan(counter, 1);

        return new SessionId(module.ToLowerInvariant(), date, counter);
    }

    /// <summary>
    /// Parses a session ID string in the format {module}-YYYYMMDD-{counter}.
    /// </summary>
    public static SessionId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var match = SessionIdPattern().Match(value);
        if (!match.Success)
        {
            throw new FormatException($"Invalid session ID format: '{value}'. Expected '{{module}}-YYYYMMDD-{{counter}}'.");
        }

        var module = match.Groups["module"].Value;
        var dateStr = match.Groups["date"].Value;
        var counterStr = match.Groups["counter"].Value;

        if (!DateOnly.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new FormatException($"Invalid date in session ID: '{dateStr}'.");
        }

        var counter = int.Parse(counterStr, CultureInfo.InvariantCulture);
        if (counter < 1)
        {
            throw new FormatException($"Session counter must be >= 1, got {counter}.");
        }

        return new SessionId(module, date, counter);
    }

    /// <summary>
    /// Tries to parse a session ID string, returning null on failure.
    /// </summary>
    public static SessionId? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return Parse(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public override string ToString() =>
        $"{Module}-{Date:yyyyMMdd}-{Counter}";

    /// <inheritdoc />
    public bool Equals(SessionId? other) =>
        other is not null && Module == other.Module && Date == other.Date && Counter == other.Counter;

    /// <inheritdoc />
    public override bool Equals(object? obj) =>
        Equals(obj as SessionId);

    /// <inheritdoc />
    public override int GetHashCode() =>
        HashCode.Combine(Module, Date, Counter);

    public static bool operator ==(SessionId? left, SessionId? right) =>
        Equals(left, right);

    public static bool operator !=(SessionId? left, SessionId? right) =>
        !Equals(left, right);

    [GeneratedRegex(@"^(?<module>[a-z][a-z0-9-]*)-(?<date>\d{8})-(?<counter>\d+)$", RegexOptions.Compiled)]
    private static partial Regex SessionIdPattern();
}
