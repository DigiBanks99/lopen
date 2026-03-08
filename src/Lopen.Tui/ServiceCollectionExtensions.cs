using Lopen.Configuration;
using Lopen.Core;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Lopen.Storage;
using Lopen.Tui.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            string historyPath = Path.Combine(configHome, "lopen", "history.txt");
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
            IAnsiConsole console = sp.GetRequiredService<IAnsiConsole>();
            IEnumerable<ISlashCommand> commands = sp.GetServices<ISlashCommand>();
            return new SlashCommandRegistry(console, commands);
        });
        services.TryAddSingleton<ISlashCommandRegistry>(sp => sp.GetRequiredService<SlashCommandRegistry>());

        // Slash command completion (requires ISlashCommandRegistry)
        services.TryAddSingleton<RadLine.ITextCompletion>(sp =>
        {
            ISlashCommandRegistry registry = sp.GetRequiredService<ISlashCommandRegistry>();
            return new SlashCommandCompletion(registry);
        });

        // Line editor
        services.TryAddSingleton<LopenLineEditor>(sp =>
        {
            IAnsiConsole console = sp.GetRequiredService<IAnsiConsole>();
            RadLine.ILineEditorHistory history = sp.GetRequiredService<RadLine.ILineEditorHistory>();
            RadLine.ITextCompletion? completion = sp.GetService<RadLine.ITextCompletion>();
            IPauseController? pauseController = sp.GetService<IPauseController>();
            return new LopenLineEditor(console, history, completion, pauseController);
        });

        // TUI output renderer - registered as IOutputRenderer to override HeadlessRenderer
        services.AddSingleton<IOutputRenderer>(sp =>
        {
            IAnsiConsole console = sp.GetRequiredService<IAnsiConsole>();
            LopenLineEditor lineEditor = sp.GetRequiredService<LopenLineEditor>();
            return new TuiOutputRenderer(console, lineEditor);
        });

        // User prompt queue for TUI-to-orchestrator communication
        services.AddSingleton<TuiUserPromptQueue>();
        services.AddSingleton<IUserPromptQueue>(sp => sp.GetRequiredService<TuiUserPromptQueue>());

        // Response rendering components
        services.TryAddSingleton<ResponseRenderer>();
        services.TryAddSingleton<ToolCallRenderer>();
        services.TryAddSingleton<StatsBar>();

        // Workflow overview block
        services.TryAddSingleton<WorkflowOverviewBlock>(sp =>
        {
            IAnsiConsole console = sp.GetRequiredService<IAnsiConsole>();
            IOptions<LopenOptions> options = sp.GetRequiredService<IOptions<LopenOptions>>();
            IWorkflowEngine? workflowEngine = sp.GetService<IWorkflowEngine>();
            ITokenTracker? tokenTracker = sp.GetService<ITokenTracker>();
            IPauseController? pauseController = sp.GetService<IPauseController>();
            return new WorkflowOverviewBlock(console, options, workflowEngine, tokenTracker, pauseController);
        });

        // TUI runner for the REPL loop
        services.TryAddSingleton<TuiRunner>(sp =>
        {
            IAnsiConsole console = sp.GetRequiredService<IAnsiConsole>();
            LopenLineEditor lineEditor = sp.GetRequiredService<LopenLineEditor>();
            TuiUserPromptQueue promptQueue = sp.GetRequiredService<TuiUserPromptQueue>();
            IOutputRenderer renderer = sp.GetRequiredService<IOutputRenderer>();
            SlashCommandRegistry commandRegistry = sp.GetRequiredService<SlashCommandRegistry>();
            IWorkflowOrchestrator? orchestrator = sp.GetService<IWorkflowOrchestrator>();
            WorkflowOverviewBlock? overviewBlock = sp.GetService<WorkflowOverviewBlock>();
            return new TuiRunner(console, lineEditor, promptQueue, renderer, commandRegistry, orchestrator, overviewBlock);
        });

        return services;
    }
}
