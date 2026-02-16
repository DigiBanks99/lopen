using Microsoft.Extensions.Logging;

namespace Lopen.Tui;

/// <summary>
/// Executes slash commands by parsing input through SlashCommandRegistry
/// and delegating to registered handlers.
/// </summary>
internal sealed class SlashCommandExecutor : ISlashCommandExecutor
{
    private readonly SlashCommandRegistry _registry;
    private readonly Dictionary<string, Func<string?, CancellationToken, Task<SlashCommandResult>>> _handlers;
    private readonly ILogger<SlashCommandExecutor> _logger;

    public SlashCommandExecutor(SlashCommandRegistry registry, ILogger<SlashCommandExecutor> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _handlers = new(StringComparer.OrdinalIgnoreCase);

        RegisterDefaultHandlers();
    }

    /// <summary>
    /// Registers a handler for a specific slash command.
    /// </summary>
    public void RegisterHandler(string command, Func<string?, CancellationToken, Task<SlashCommandResult>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[command] = handler;
    }

    public async Task<SlashCommandResult> ExecuteAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return SlashCommandResult.Error(string.Empty, "Empty command");

        if (!input.StartsWith('/'))
            return SlashCommandResult.Error(input, "Not a slash command");

        var def = _registry.TryParse(input);
        if (def is null)
        {
            var commandToken = input.Split(' ', 2)[0];
            _logger.LogDebug("Unknown slash command: {Command}", commandToken);
            return SlashCommandResult.UnknownCommand(commandToken, _registry.GetAll());
        }

        var args = input.Length > def.Command.Length
            ? input[def.Command.Length..].Trim()
            : null;

        if (string.IsNullOrEmpty(args))
            args = null;

        if (_handlers.TryGetValue(def.Command, out var handler))
        {
            try
            {
                _logger.LogDebug("Executing slash command: {Command} {Args}", def.Command, args);
                return await handler(args, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error executing slash command: {Command}", def.Command);
                return SlashCommandResult.Error(def.Command, $"Command failed: {ex.Message}");
            }
        }

        return SlashCommandResult.Success(def.Command, $"{def.Description}", args);
    }

    private void RegisterDefaultHandlers()
    {
        RegisterHandler("/help", HandleHelpAsync);
    }

    private Task<SlashCommandResult> HandleHelpAsync(string? args, CancellationToken ct)
    {
        var commands = _registry.GetAll();
        var lines = commands.Select(c =>
        {
            var alias = c.Alias is not null ? $" ({c.Alias})" : string.Empty;
            return $"  {c.Command,-12}{alias,-8} {c.Description}";
        });
        var helpText = "Available commands:\n" + string.Join("\n", lines);
        return Task.FromResult(SlashCommandResult.Success("/help", helpText));
    }
}
