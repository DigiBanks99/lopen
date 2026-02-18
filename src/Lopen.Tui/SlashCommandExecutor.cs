using Lopen.Auth;
using Lopen.Configuration;
using Lopen.Core.Git;
using Lopen.Core.Workflow;
using Lopen.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly IServiceProvider? _serviceProvider;

    public SlashCommandExecutor(SlashCommandRegistry registry, ILogger<SlashCommandExecutor> logger,
        IServiceProvider? serviceProvider = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider;
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

        if (_serviceProvider is null)
            return;

        RegisterHandler("/spec", (args, ct) => HandlePhaseAsync("/spec", args, ct));
        RegisterHandler("/plan", (args, ct) => HandlePhaseAsync("/plan", args, ct));
        RegisterHandler("/build", (args, ct) => HandlePhaseAsync("/build", args, ct));
        RegisterHandler("/session", HandleSessionAsync);
        RegisterHandler("/config", HandleConfigAsync);
        RegisterHandler("/revert", HandleRevertAsync);
        RegisterHandler("/auth", HandleAuthAsync);
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

    private async Task<SlashCommandResult> HandlePhaseAsync(string command, string? args, CancellationToken ct)
    {
        var orchestrator = _serviceProvider!.GetService<IWorkflowOrchestrator>();
        if (orchestrator is null)
            return SlashCommandResult.Error(command, "No project found — orchestrator not available");

        var sessionManager = _serviceProvider!.GetService<ISessionManager>();
        var moduleName = args;

        if (string.IsNullOrWhiteSpace(moduleName) && sessionManager is not null)
        {
            var latestId = await sessionManager.GetLatestSessionIdAsync(ct).ConfigureAwait(false);
            if (latestId is not null)
            {
                var state = await sessionManager.LoadSessionStateAsync(latestId, ct).ConfigureAwait(false);
                moduleName = state?.Module;
            }
        }

        if (string.IsNullOrWhiteSpace(moduleName))
        {
            var selector = _serviceProvider!.GetService<IModuleSelectionService>();
            if (selector is not null)
                moduleName = await selector.SelectModuleAsync(ct).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(moduleName))
            return SlashCommandResult.Error(command, "No module specified and no active session found. Usage: " + command + " <module>");

        _logger.LogInformation("Slash command {Command} starting orchestration for module {Module}", command, moduleName);
        var result = await orchestrator.RunAsync(moduleName, null, ct).ConfigureAwait(false);

        var summary = result.Summary ?? (result.IsComplete ? "Completed" : "Interrupted");
        return SlashCommandResult.Success(command, summary);
    }

    private async Task<SlashCommandResult> HandleSessionAsync(string? args, CancellationToken ct)
    {
        var sessionManager = _serviceProvider!.GetService<ISessionManager>();
        if (sessionManager is null)
            return SlashCommandResult.Error("/session", "No project found — session manager not available");

        var subcommand = args?.Split(' ', 2)[0]?.ToLowerInvariant();
        var subArgs = args is not null && args.Contains(' ') ? args.Split(' ', 2)[1] : null;

        return subcommand switch
        {
            "list" or null => await HandleSessionListAsync(sessionManager, ct).ConfigureAwait(false),
            "show" => await HandleSessionShowAsync(sessionManager, subArgs, ct).ConfigureAwait(false),
            "resume" => await HandleSessionResumeAsync(sessionManager, subArgs, ct).ConfigureAwait(false),
            "delete" => await HandleSessionDeleteAsync(sessionManager, subArgs, ct).ConfigureAwait(false),
            "prune" => await HandleSessionPruneAsync(sessionManager, ct).ConfigureAwait(false),
            _ => SlashCommandResult.Error("/session", $"Unknown subcommand: {subcommand}. Valid: list, show, resume, delete, prune")
        };
    }

    private static async Task<SlashCommandResult> HandleSessionListAsync(
        ISessionManager sessionManager, CancellationToken ct)
    {
        var sessions = await sessionManager.ListSessionsAsync(ct).ConfigureAwait(false);
        if (sessions.Count == 0)
            return SlashCommandResult.Success("/session", "No sessions found");

        var latestId = await sessionManager.GetLatestSessionIdAsync(ct).ConfigureAwait(false);
        var lines = sessions.Select(s =>
        {
            var marker = s.Equals(latestId) ? " *" : "";
            return $"  {s}{marker}";
        });
        return SlashCommandResult.Success("/session", $"Sessions ({sessions.Count}):\n{string.Join("\n", lines)}");
    }

    private static async Task<SlashCommandResult> HandleSessionShowAsync(
        ISessionManager sessionManager, string? sessionIdStr, CancellationToken ct)
    {
        SessionId? sessionId;
        if (string.IsNullOrWhiteSpace(sessionIdStr))
        {
            sessionId = await sessionManager.GetLatestSessionIdAsync(ct).ConfigureAwait(false);
            if (sessionId is null)
                return SlashCommandResult.Error("/session", "No active session. Usage: /session show <session-id>");
        }
        else
        {
            sessionId = SessionId.TryParse(sessionIdStr);
            if (sessionId is null)
                return SlashCommandResult.Error("/session", $"Invalid session ID: {sessionIdStr}");
        }

        var state = await sessionManager.LoadSessionStateAsync(sessionId, ct).ConfigureAwait(false);
        if (state is null)
            return SlashCommandResult.Error("/session", $"Session not found: {sessionId}");

        var metrics = await sessionManager.LoadSessionMetricsAsync(sessionId, ct).ConfigureAwait(false);
        var lines = new List<string>
        {
            $"Session: {state.SessionId}",
            $"Module: {state.Module}",
            $"Phase: {state.Phase}",
            $"Step: {state.Step}",
            $"Complete: {state.IsComplete}",
            $"Created: {state.CreatedAt:g}",
            $"Updated: {state.UpdatedAt:g}"
        };
        if (state.Component is not null)
            lines.Add($"Component: {state.Component}");
        if (metrics is not null)
        {
            lines.Add($"Iterations: {metrics.IterationCount}");
            lines.Add($"Tokens: {metrics.CumulativeInputTokens + metrics.CumulativeOutputTokens}");
        }

        return SlashCommandResult.Success("/session", string.Join("\n", lines));
    }

    private static async Task<SlashCommandResult> HandleSessionResumeAsync(
        ISessionManager sessionManager, string? sessionIdStr, CancellationToken ct)
    {
        SessionId? sessionId;
        if (string.IsNullOrWhiteSpace(sessionIdStr))
        {
            sessionId = await sessionManager.GetLatestSessionIdAsync(ct).ConfigureAwait(false);
            if (sessionId is null)
                return SlashCommandResult.Error("/session", "No session to resume. Usage: /session resume <session-id>");
        }
        else
        {
            sessionId = SessionId.TryParse(sessionIdStr);
            if (sessionId is null)
                return SlashCommandResult.Error("/session", $"Invalid session ID: {sessionIdStr}");
        }

        var state = await sessionManager.LoadSessionStateAsync(sessionId, ct).ConfigureAwait(false);
        if (state?.IsComplete == true)
            return SlashCommandResult.Error("/session", $"Cannot resume completed session: {sessionId}");

        await sessionManager.SetLatestAsync(sessionId, ct).ConfigureAwait(false);
        return SlashCommandResult.Success("/session", $"Resumed session: {sessionId}");
    }

    private static async Task<SlashCommandResult> HandleSessionDeleteAsync(
        ISessionManager sessionManager, string? sessionIdStr, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionIdStr))
            return SlashCommandResult.Error("/session", "Usage: /session delete <session-id>");

        var sessionId = SessionId.TryParse(sessionIdStr);
        if (sessionId is null)
            return SlashCommandResult.Error("/session", $"Invalid session ID: {sessionIdStr}");

        await sessionManager.DeleteSessionAsync(sessionId, ct).ConfigureAwait(false);
        return SlashCommandResult.Success("/session", $"Deleted session: {sessionId}");
    }

    private async Task<SlashCommandResult> HandleSessionPruneAsync(
        ISessionManager sessionManager, CancellationToken ct)
    {
        var options = _serviceProvider!.GetService<IOptions<LopenOptions>>();
        var retention = options?.Value?.Session?.SessionRetention ?? 10;
        var pruned = await sessionManager.PruneSessionsAsync(retention, ct).ConfigureAwait(false);
        return SlashCommandResult.Success("/session", $"Pruned {pruned} session(s)");
    }

    private async Task<SlashCommandResult> HandleConfigAsync(string? args, CancellationToken ct)
    {
        var configRoot = _serviceProvider!.GetService<IConfigurationRoot>();
        if (configRoot is null)
            return SlashCommandResult.Error("/config", "Configuration not available");

        var entries = ConfigurationDiagnostics.GetEntries(configRoot);
        var isJson = args?.Contains("--json", StringComparison.OrdinalIgnoreCase) == true;
        var output = isJson
            ? ConfigurationDiagnostics.FormatJson(entries)
            : ConfigurationDiagnostics.Format(entries);

        return SlashCommandResult.Success("/config", output);
    }

    private async Task<SlashCommandResult> HandleRevertAsync(string? args, CancellationToken ct)
    {
        var revertService = _serviceProvider!.GetService<IRevertService>();
        var sessionManager = _serviceProvider!.GetService<ISessionManager>();

        if (revertService is null)
            return SlashCommandResult.Error("/revert", "No project found — revert service not available");
        if (sessionManager is null)
            return SlashCommandResult.Error("/revert", "No project found — session manager not available");

        var latestId = await sessionManager.GetLatestSessionIdAsync(ct).ConfigureAwait(false);
        if (latestId is null)
            return SlashCommandResult.Error("/revert", "No active session to revert");

        var state = await sessionManager.LoadSessionStateAsync(latestId, ct).ConfigureAwait(false);
        if (state?.LastTaskCompletionCommitSha is null)
            return SlashCommandResult.Error("/revert", "No checkpoint found in current session");

        var result = await revertService.RevertToCommitAsync(state.LastTaskCompletionCommitSha, ct).ConfigureAwait(false);
        if (!result.Success)
            return SlashCommandResult.Error("/revert", result.Message);

        var updatedState = state with { LastTaskCompletionCommitSha = null };
        await sessionManager.SaveSessionStateAsync(latestId, updatedState, ct).ConfigureAwait(false);

        return SlashCommandResult.Success("/revert", result.Message);
    }

    private async Task<SlashCommandResult> HandleAuthAsync(string? args, CancellationToken ct)
    {
        var authService = _serviceProvider!.GetService<IAuthService>();
        if (authService is null)
            return SlashCommandResult.Error("/auth", "Authentication service not available");

        var subcommand = args?.Split(' ', 2)[0]?.ToLowerInvariant();

        switch (subcommand)
        {
            case "login":
                await authService.LoginAsync(ct).ConfigureAwait(false);
                return SlashCommandResult.Success("/auth", "Login successful");

            case "logout":
                await authService.LogoutAsync(ct).ConfigureAwait(false);
                return SlashCommandResult.Success("/auth", "Logged out");

            case "status" or null:
                var status = await authService.GetStatusAsync(ct).ConfigureAwait(false);
                var statusLines = new List<string>
                {
                    $"State: {status.State}",
                    $"Source: {status.Source}"
                };
                if (status.Username is not null)
                    statusLines.Add($"User: {status.Username}");
                if (status.ErrorMessage is not null)
                    statusLines.Add($"Error: {status.ErrorMessage}");
                return SlashCommandResult.Success("/auth", string.Join("\n", statusLines));

            default:
                return SlashCommandResult.Error("/auth", $"Unknown subcommand: {subcommand}. Valid: login, status, logout");
        }
    }
}
