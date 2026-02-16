namespace Lopen.Tui;

/// <summary>
/// Result of executing a slash command.
/// </summary>
public sealed record SlashCommandResult
{
    /// <summary>Whether the command executed successfully.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Output message to display in the activity panel.</summary>
    public string? OutputMessage { get; init; }

    /// <summary>Error message when the command is unknown or fails.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>The command that was parsed (null if unrecognized).</summary>
    public string? Command { get; init; }

    /// <summary>The arguments passed to the command.</summary>
    public string? Arguments { get; init; }

    public static SlashCommandResult Success(string command, string message, string? arguments = null) =>
        new() { IsSuccess = true, Command = command, OutputMessage = message, Arguments = arguments };

    public static SlashCommandResult UnknownCommand(string input, IReadOnlyList<SlashCommandDefinition> validCommands)
    {
        var commandList = string.Join(", ", validCommands.Select(c => c.Command));
        return new()
        {
            IsSuccess = false,
            Command = input,
            ErrorMessage = $"Unknown command: {input}. Valid commands: {commandList}"
        };
    }

    public static SlashCommandResult Error(string command, string message) =>
        new() { IsSuccess = false, Command = command, ErrorMessage = message };
}
