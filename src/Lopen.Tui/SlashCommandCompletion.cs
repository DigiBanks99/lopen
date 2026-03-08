using RadLine;

namespace Lopen.Tui;

/// <summary>
/// Provides tab completion for slash commands using the slash command registry.
/// </summary>
public sealed class SlashCommandCompletion : ITextCompletion
{
    private readonly ISlashCommandRegistry _registry;

    public SlashCommandCompletion(ISlashCommandRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public IEnumerable<string>? GetCompletions(string prefix, string word, string suffix)
    {
        // Only complete slash commands
        var fullInput = prefix + word;
        if (!fullInput.StartsWith('/'))
            return null;

        var commandPrefix = fullInput[1..]; // Remove the leading /
        var commands = _registry.GetCommands();

        var matches = commands
            .Where(c => c.Name.StartsWith(commandPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(c => "/" + c.Name)
            .ToList();

        return matches.Count > 0 ? matches : null;
    }
}
