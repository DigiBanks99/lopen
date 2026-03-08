using Lopen.Core;
using Lopen.Core.Workflow;
using Lopen.Storage;
using Lopen.Tui.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// Extension methods for registering TUI services.
/// Must be called BEFORE AddLopenCore() so that TryAddSingleton&lt;IOutputRenderer&gt;
/// in core becomes a no-op, allowing TuiOutputRenderer to take precedence.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Lopen TUI services to the service collection.
    /// </summary>
    public static IServiceCollection AddLopenTui(this IServiceCollection services)
    {
        // Register IAnsiConsole for Spectre.Console rendering
        services.TryAddSingleton<IAnsiConsole>(AnsiConsole.Console);

        // File-backed history stored in user's config directory
        services.TryAddSingleton<RadLine.ILineEditorHistory>(sp =>
        {
            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            var historyPath = Path.Combine(configHome, "lopen", "history.txt");
            return new FileLineEditorHistory(historyPath);
        });

        // Slash commands
        services.AddSingleton<ISlashCommand>(sp => new HelpCommand(
            sp.GetRequiredService<IAnsiConsole>(),
            new Lazy<ISlashCommandRegistry>(() => sp.GetRequiredService<ISlashCommandRegistry>())));
        services.AddSingleton<ISlashCommand, ModelCommand>();
        services.AddSingleton<ISlashCommand, SkillsCommand>();
        services.AddSingleton<ISlashCommand>(sp => new SessionsCommand(
            sp.GetRequiredService<IAnsiConsole>(),
            sp.GetService<ISessionManager>()));
        services.AddSingleton<ISlashCommand>(sp => new ResumeCommand(
            sp.GetRequiredService<IAnsiConsole>(),
            sp.GetService<ISessionManager>()));
        services.AddSingleton<ISlashCommand, ClearCommand>();
        services.AddSingleton<ISlashCommand, ExitCommand>();

        // Command registry (also implements ISlashCommandRegistry for completion)
        services.TryAddSingleton<SlashCommandRegistry>(sp =>
        {
            var console = sp.GetRequiredService<IAnsiConsole>();
            var commands = sp.GetServices<ISlashCommand>();
            return new SlashCommandRegistry(console, commands);
        });
        services.TryAddSingleton<ISlashCommandRegistry>(sp => sp.GetRequiredService<SlashCommandRegistry>());

        // Slash command completion (requires ISlashCommandRegistry)
        services.TryAddSingleton<RadLine.ITextCompletion>(sp =>
        {
            var registry = sp.GetRequiredService<ISlashCommandRegistry>();
            return new SlashCommandCompletion(registry);
        });

        // Line editor
        services.TryAddSingleton<LopenLineEditor>(sp =>
        {
            var console = sp.GetRequiredService<IAnsiConsole>();
            var history = sp.GetRequiredService<RadLine.ILineEditorHistory>();
            var completion = sp.GetService<RadLine.ITextCompletion>();
            return new LopenLineEditor(console, history, completion);
        });

        // TUI output renderer - registered as IOutputRenderer to override HeadlessRenderer
        services.AddSingleton<IOutputRenderer>(sp =>
        {
            var console = sp.GetRequiredService<IAnsiConsole>();
            var lineEditor = sp.GetRequiredService<LopenLineEditor>();
            return new TuiOutputRenderer(console, lineEditor);
        });

        // User prompt queue for TUI-to-orchestrator communication
        services.AddSingleton<TuiUserPromptQueue>();
        services.AddSingleton<IUserPromptQueue>(sp => sp.GetRequiredService<TuiUserPromptQueue>());

        // TUI runner for the REPL loop
        services.TryAddSingleton<TuiRunner>(sp =>
        {
            var console = sp.GetRequiredService<IAnsiConsole>();
            var lineEditor = sp.GetRequiredService<LopenLineEditor>();
            var promptQueue = sp.GetRequiredService<TuiUserPromptQueue>();
            var renderer = sp.GetRequiredService<IOutputRenderer>();
            var commandRegistry = sp.GetRequiredService<SlashCommandRegistry>();
            var orchestrator = sp.GetService<IWorkflowOrchestrator>();
            return new TuiRunner(console, lineEditor, promptQueue, renderer, commandRegistry, orchestrator);
        });

        return services;
    }
}
