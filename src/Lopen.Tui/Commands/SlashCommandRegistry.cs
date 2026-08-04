using Spectre.Console;

namespace Lopen.Tui.Commands;

/// <summary>
/// Registry and dispatcher for slash commands.
/// </summary>
public sealed class SlashCommandRegistry : ISlashCommandRegistry
{
    private readonly Dictionary<string, ISlashCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly IAnsiConsole _console;

    public SlashCommandRegistry(IAnsiConsole console, IEnumerable<ISlashCommand> commands)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        foreach (ISlashCommand cmd in commands)
        {
            _commands[cmd.Name] = cmd;
        }
    }

    public IReadOnlyList<SlashCommandDescriptor> GetCommands()
    {
        return _commands.Values
            .Select(c => new SlashCommandDescriptor(c.Name, c.Description))
            .OrderBy(c => c.Name)
            .ToList();
    }

    /// <summary>
    /// Dispatches input to the appropriate slash command.
    /// </summary>
    public async Task<SlashCommandResult> DispatchAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith('/'))
            return SlashCommandResult.NotACommand;

        string trimmed = input[1..];
        int spaceIndex = trimmed.IndexOf(' ');
        string name = spaceIndex >= 0 ? trimmed[..spaceIndex] : trimmed;
        string args = spaceIndex >= 0 ? trimmed[(spaceIndex + 1)..].Trim() : string.Empty;

        if (_commands.TryGetValue(name, out ISlashCommand? command))
        {
            return await command.ExecuteAsync(args, cancellationToken);
        }

        _console.MarkupLine(LopenTheme.Styled(
            $"Unknown command: /{Markup.Escape(name)}. Type /help for available commands.", LopenTheme.Warning));
        return SlashCommandResult.Handled;
    }
}
