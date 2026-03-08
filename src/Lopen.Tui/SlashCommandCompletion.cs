using RadLine;

namespace Lopen.Tui;

/// <summary>
/// Provides tab completion for slash commands using the slash command registry.
/// </summary>
public sealed class SlashCommandCompletion(ISlashCommandRegistry registry) : ITextCompletion
{
    private readonly ISlashCommandRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public IEnumerable<string>? GetCompletions(string prefix, string word, string suffix)
    {
        // Only complete slash commands
        string fullInput = prefix + word;
        if (!fullInput.StartsWith('/'))
            return null;

        string commandPrefix = fullInput[1..]; // Remove the leading /
        IReadOnlyList<SlashCommandDescriptor> commands = _registry.GetCommands();

        List<string> matches = [.. commands
            .Where(c => c.Name.StartsWith(commandPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(c => "/" + c.Name)];

        return matches.Count > 0 ? matches : null;
    }
}
