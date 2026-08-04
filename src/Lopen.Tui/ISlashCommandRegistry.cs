namespace Lopen.Tui;

/// <summary>
/// Registry of available slash commands for the TUI.
/// </summary>
public interface ISlashCommandRegistry
{
    /// <summary>
    /// Gets all registered slash command descriptors.
    /// </summary>
    IReadOnlyList<SlashCommandDescriptor> GetCommands();
}

/// <summary>
/// Describes a slash command for display and completion.
/// </summary>
/// <param name="Name">Command name without the leading slash (e.g., "help").</param>
/// <param name="Description">Human-readable description of the command.</param>
public sealed record SlashCommandDescriptor(string Name, string Description);


