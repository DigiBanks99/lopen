namespace Lopen.Tui;

/// <summary>
/// Slash command registry and parser.
/// </summary>
public sealed class SlashCommandRegistry
{
    private readonly Dictionary<string, SlashCommandDefinition> _commands = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a slash command.
    /// </summary>
    public void Register(string command, string description, string? alias = null)
    {
        var def = new SlashCommandDefinition(command, description, alias);
        _commands[command] = def;
        if (alias is not null)
            _commands[alias] = def;
    }

    /// <summary>
    /// Tries to parse a slash command from input text.
    /// Returns the command if recognized, null otherwise.
    /// </summary>
    public SlashCommandDefinition? TryParse(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith('/'))
            return null;

        var parts = input.Split(' ', 2);
        var cmd = parts[0];

        return _commands.TryGetValue(cmd, out var def) ? def : null;
    }

    /// <summary>
    /// Gets all registered commands (unique by primary name).
    /// </summary>
    public IReadOnlyList<SlashCommandDefinition> GetAll()
        => _commands.Values.DistinctBy(d => d.Command).OrderBy(d => d.Command).ToList();

    /// <summary>
    /// Creates a registry with the default Lopen slash commands.
    /// </summary>
    public static SlashCommandRegistry CreateDefault()
    {
        var registry = new SlashCommandRegistry();
        registry.Register("/help", "Show available commands");
        registry.Register("/spec", "Start requirement gathering");
        registry.Register("/plan", "Start planning mode");
        registry.Register("/build", "Start build mode");
        registry.Register("/session", "Manage sessions");
        registry.Register("/config", "Show configuration");
        registry.Register("/revert", "Revert to last checkpoint");
        registry.Register("/auth", "Authentication commands");
        return registry;
    }
}

/// <summary>
/// A registered slash command definition.
/// </summary>
public sealed record SlashCommandDefinition(string Command, string Description, string? Alias = null);
