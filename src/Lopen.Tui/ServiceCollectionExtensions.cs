using Lopen.Core;
using Lopen.Core.Workflow;
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

        // Slash command completion (requires ISlashCommandRegistry)
        services.TryAddSingleton<RadLine.ITextCompletion>(sp =>
        {
            var registry = sp.GetService<ISlashCommandRegistry>();
            if (registry is not null)
                return new SlashCommandCompletion(registry);
            return new SlashCommandCompletion(new EmptySlashCommandRegistry());
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
            var orchestrator = sp.GetService<IWorkflowOrchestrator>();
            return new TuiRunner(console, lineEditor, promptQueue, renderer, orchestrator);
        });

        return services;
    }
}
