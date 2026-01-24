using System.Text.Json;

namespace Lopen.Core;

/// <summary>
/// Represents information about a CLI command.
/// </summary>
public record CommandInfo(string Name, string Description, IReadOnlyList<CommandInfo>? Subcommands = null);

/// <summary>
/// Service for formatting help/command information.
/// </summary>
public class HelpService
{
    /// <summary>
    /// Formats the command list as text.
    /// </summary>
    public string FormatCommandListAsText(string appName, string appDescription, IEnumerable<CommandInfo> commands)
    {
        var lines = new List<string>
        {
            $"{appName} - {appDescription}",
            "",
            "Commands:"
        };

        foreach (var cmd in commands)
        {
            lines.Add($"  {cmd.Name,-15} {cmd.Description}");
        }

        lines.Add("");
        lines.Add("Use 'lopen help <command>' for more information about a command.");

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Formats the command list as JSON.
    /// </summary>
    public string FormatCommandListAsJson(string appName, string appDescription, IEnumerable<CommandInfo> commands)
    {
        var obj = new
        {
            name = appName,
            description = appDescription,
            commands = commands.Select(c => new
            {
                name = c.Name,
                description = c.Description
            }).ToArray()
        };

        return JsonSerializer.Serialize(obj);
    }

    /// <summary>
    /// Formats a single command's help as text.
    /// </summary>
    public string FormatCommandHelpAsText(CommandInfo command)
    {
        var lines = new List<string>
        {
            $"{command.Name} - {command.Description}"
        };

        if (command.Subcommands is { Count: > 0 })
        {
            lines.Add("");
            lines.Add("Subcommands:");
            foreach (var sub in command.Subcommands)
            {
                lines.Add($"  {sub.Name,-15} {sub.Description}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Formats a single command's help as JSON.
    /// </summary>
    public string FormatCommandHelpAsJson(CommandInfo command)
    {
        var obj = new
        {
            name = command.Name,
            description = command.Description,
            subcommands = command.Subcommands?.Select(s => new
            {
                name = s.Name,
                description = s.Description
            }).ToArray()
        };

        return JsonSerializer.Serialize(obj);
    }
}
