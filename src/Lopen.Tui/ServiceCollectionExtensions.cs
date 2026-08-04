using Lopen.Configuration;
using Lopen.Core;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Lopen.Storage;
using Lopen.Tui.Commands;
using Lopen.Tui.Gallery;
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
        services.AddSingleton<ISlashCommand, HelpCommand>();
        services.AddSingleton<ISlashCommand, ModelCommand>();
        services.AddSingleton<ISlashCommand, SkillsCommand>();
        services.AddSingleton<ISlashCommand, SessionsCommand>();
        services.AddSingleton<ISlashCommand, ResumeCommand>();
        services.AddSingleton<ISlashCommand, ClearCommand>();
        services.AddSingleton<ISlashCommand, ExitCommand>();
        services.AddSingleton(sp => new Lazy<ISlashCommandRegistry>(() => sp.GetRequiredService<ISlashCommandRegistry>()));

        // Command registry (also implements ISlashCommandRegistry for completion)
        services.TryAddSingleton<SlashCommandRegistry>();
        services.TryAddSingleton<ISlashCommandRegistry, SlashCommandRegistry>();

        // Slash command completion (requires ISlashCommandRegistry)
        services.TryAddSingleton<RadLine.ITextCompletion, SlashCommandCompletion>();

        // Line editor
        services.TryAddSingleton<LopenLineEditor>();
        services.TryAddSingleton(sp => new Lazy<LopenLineEditor>(() => sp.GetRequiredService<LopenLineEditor>()));

        // TUI output renderer - registered as IOutputRenderer to override HeadlessRenderer
        services.AddSingleton<IOutputRenderer, TuiOutputRenderer>();

        // User prompt queue for TUI-to-orchestrator communication
        services.AddSingleton<TuiUserPromptQueue>();
        services.AddSingleton<IUserPromptQueue, TuiUserPromptQueue>();

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

        // Command palette for command discovery (TUI-10 through TUI-13)
        services.TryAddSingleton<CommandPalette>(sp =>
        {
            IAnsiConsole console = sp.GetRequiredService<IAnsiConsole>();
            ISlashCommandRegistry registry = sp.GetRequiredService<ISlashCommandRegistry>();
            return new CommandPalette(console, registry);
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
            CommandPalette? commandPalette = sp.GetService<CommandPalette>();
            ISessionManager? sessionManager = sp.GetService<ISessionManager>();
            IModuleSelectionService? moduleSelectionService = sp.GetService<IModuleSelectionService>();
            return new TuiRunner(console, lineEditor, promptQueue, renderer, commandRegistry, orchestrator, overviewBlock, commandPalette, sessionManager, moduleSelectionService);
        });

        // Gallery components for visual testing (TUI-34 through TUI-36)
        services.AddSingleton<IGalleryComponent, WorkflowOverviewGalleryComponent>();
        services.AddSingleton<IGalleryComponent, PromptInputGalleryComponent>();
        services.AddSingleton<IGalleryComponent, CommandPaletteGalleryComponent>();
        services.AddSingleton<IGalleryComponent, ResponseRenderingGalleryComponent>();
        services.AddSingleton<IGalleryComponent, SessionListGalleryComponent>();
        services.AddSingleton<IGalleryComponent, ErrorPanelGalleryComponent>();
        services.AddSingleton<IGalleryComponent, HelpOutputGalleryComponent>();

        return services;
    }
}
