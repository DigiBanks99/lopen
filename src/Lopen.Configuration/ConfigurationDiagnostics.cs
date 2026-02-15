using Microsoft.Extensions.Configuration;

namespace Lopen.Configuration;

/// <summary>
/// Produces diagnostic output showing each configuration setting's resolved value and source.
/// Used by <c>lopen config show</c>.
/// </summary>
public static class ConfigurationDiagnostics
{
    /// <summary>
    /// Returns a list of (Key, Value, Provider) tuples for all configuration entries.
    /// </summary>
    public static IReadOnlyList<ConfigurationEntry> GetEntries(IConfigurationRoot configurationRoot)
    {
        ArgumentNullException.ThrowIfNull(configurationRoot);

        var entries = new List<ConfigurationEntry>();

        foreach (var kvp in configurationRoot.AsEnumerable())
        {
            if (kvp.Value is null)
                continue;

            var providerName = GetProviderName(configurationRoot, kvp.Key);
            entries.Add(new ConfigurationEntry(kvp.Key, kvp.Value, providerName));
        }

        return entries;
    }

    /// <summary>
    /// Formats configuration entries as a human-readable table.
    /// </summary>
    public static string Format(IReadOnlyList<ConfigurationEntry> entries)
    {
        if (entries.Count == 0)
            return "No configuration entries found.";

        var keyWidth = Math.Max("Setting".Length, entries.Max(e => e.Key.Length));
        var valueWidth = Math.Max("Value".Length, entries.Max(e => e.Value.Length));

        var header = $"{"Setting".PadRight(keyWidth)}  {"Value".PadRight(valueWidth)}  Source";
        var separator = new string('-', header.Length);

        var lines = new List<string> { header, separator };
        foreach (var entry in entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
        {
            lines.Add($"{entry.Key.PadRight(keyWidth)}  {entry.Value.PadRight(valueWidth)}  {entry.Source}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Formats configuration entries as machine-readable JSON.
    /// </summary>
    public static string FormatJson(IReadOnlyList<ConfigurationEntry> entries)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[");

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var comma = i < entries.Count - 1 ? "," : "";
            sb.AppendLine($"  {{\"key\": {JsonEscape(entry.Key)}, \"value\": {JsonEscape(entry.Value)}, \"source\": {JsonEscape(entry.Source)}}}{comma}");
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static string JsonEscape(string value)
    {
        return "\"" + value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t") + "\"";
    }

    private static string GetProviderName(IConfigurationRoot root, string key)
    {
        // Walk providers in reverse order (highest priority first) to find the winning provider
        for (var i = root.Providers.Count() - 1; i >= 0; i--)
        {
            var provider = root.Providers.ElementAt(i);
            if (provider.TryGet(key, out _))
            {
                return provider switch
                {
                    Microsoft.Extensions.Configuration.Json.JsonConfigurationProvider json =>
                        json.Source.Path ?? "JSON",
                    Microsoft.Extensions.Configuration.EnvironmentVariables.EnvironmentVariablesConfigurationProvider =>
                        "Environment",
                    Microsoft.Extensions.Configuration.Memory.MemoryConfigurationProvider =>
                        "CLI Override",
                    _ => provider.GetType().Name
                };
            }
        }

        return "Default";
    }
}

public sealed record ConfigurationEntry(string Key, string Value, string Source);
