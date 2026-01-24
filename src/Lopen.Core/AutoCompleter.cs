namespace Lopen.Core;

/// <summary>
/// Represents a completion suggestion.
/// </summary>
public record CompletionItem(string Text, string? Description = null);

/// <summary>
/// Interface for auto-completion providers.
/// </summary>
public interface IAutoCompleter
{
    /// <summary>
    /// Gets completion suggestions for the given input.
    /// </summary>
    /// <param name="input">Current input text.</param>
    /// <param name="cursorPosition">Cursor position in the input.</param>
    /// <returns>List of completion suggestions.</returns>
    IReadOnlyList<CompletionItem> GetCompletions(string input, int cursorPosition);
}

/// <summary>
/// Auto-completion provider for CLI commands.
/// </summary>
public class CommandAutoCompleter : IAutoCompleter
{
    private readonly List<CommandDefinition> _commands = [];

    /// <summary>
    /// Registers a command for completion.
    /// </summary>
    public void RegisterCommand(string name, string? description = null, IEnumerable<string>? subcommands = null, IEnumerable<string>? options = null)
    {
        _commands.Add(new CommandDefinition(
            name, 
            description, 
            subcommands?.ToList() ?? [],
            options?.ToList() ?? []
        ));
    }

    /// <summary>
    /// Registers multiple commands at once.
    /// </summary>
    public void RegisterCommands(IEnumerable<CommandDefinition> commands)
    {
        _commands.AddRange(commands);
    }

    /// <summary>
    /// Gets all registered commands.
    /// </summary>
    public IReadOnlyList<CommandDefinition> Commands => _commands.AsReadOnly();

    public IReadOnlyList<CompletionItem> GetCompletions(string input, int cursorPosition)
    {
        if (string.IsNullOrEmpty(input))
        {
            // Return all top-level commands
            return _commands
                .Select(c => new CompletionItem(c.Name, c.Description))
                .ToList();
        }

        var parts = input[..cursorPosition].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
        {
            return _commands
                .Select(c => new CompletionItem(c.Name, c.Description))
                .ToList();
        }

        var firstPart = parts[0];
        
        // Check if input ends with space (completing next token)
        var completingNextToken = input.Length > 0 && 
                                  cursorPosition > 0 && 
                                  input[cursorPosition - 1] == ' ';

        // If only one part and not completing next token, complete commands
        if (parts.Length == 1 && !completingNextToken)
        {
            return _commands
                .Where(c => c.Name.StartsWith(firstPart, StringComparison.OrdinalIgnoreCase))
                .Select(c => new CompletionItem(c.Name, c.Description))
                .ToList();
        }

        // Find the matching command
        var matchedCommand = _commands
            .FirstOrDefault(c => c.Name.Equals(firstPart, StringComparison.OrdinalIgnoreCase));

        if (matchedCommand == null)
        {
            return [];
        }

        // If completing next token after command, show subcommands and options
        if (completingNextToken)
        {
            var completions = new List<CompletionItem>();
            
            // Add subcommands if at correct position
            if (parts.Length == 1)
            {
                completions.AddRange(matchedCommand.Subcommands
                    .Select(s => new CompletionItem(s)));
            }
            
            // Add options
            completions.AddRange(matchedCommand.Options
                .Select(o => new CompletionItem(o)));
            
            return completions;
        }

        // Complete subcommand or option
        var currentToken = parts[^1];
        
        // Check if it's an option (starts with -)
        if (currentToken.StartsWith('-'))
        {
            return matchedCommand.Options
                .Where(o => o.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
                .Select(o => new CompletionItem(o))
                .ToList();
        }

        // Complete subcommand
        if (parts.Length == 2)
        {
            return matchedCommand.Subcommands
                .Where(s => s.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
                .Select(s => new CompletionItem(s))
                .ToList();
        }

        // For nested subcommands, look for matching subcommand's options
        var matchedSubcommand = parts.Length >= 2 ? parts[1] : null;
        if (matchedSubcommand != null && matchedCommand.Subcommands.Contains(matchedSubcommand, StringComparer.OrdinalIgnoreCase))
        {
            // Return options for the subcommand context
            return matchedCommand.Options
                .Where(o => o.StartsWith(currentToken, StringComparison.OrdinalIgnoreCase))
                .Select(o => new CompletionItem(o))
                .ToList();
        }

        return [];
    }
}

/// <summary>
/// Represents a command definition for auto-completion.
/// </summary>
public record CommandDefinition(
    string Name, 
    string? Description = null, 
    List<string>? Subcommands = null,
    List<string>? Options = null)
{
    public List<string> Subcommands { get; } = Subcommands ?? [];
    public List<string> Options { get; } = Options ?? [];
}
