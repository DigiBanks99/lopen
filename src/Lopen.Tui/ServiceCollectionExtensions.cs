using Lopen.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Tui;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers TUI services including the application lifecycle and component gallery
    /// with all built-in components self-registered.
    /// </summary>
    public static IServiceCollection AddLopenTui(this IServiceCollection services)
    {
        services.AddSingleton<TopPanelComponent>();
        services.AddSingleton<ActivityPanelComponent>();
        services.AddSingleton<ContextPanelComponent>();
        services.AddSingleton<PromptAreaComponent>();
        services.AddSingleton<KeyboardHandler>();
        services.AddSingleton(_ => SlashCommandRegistry.CreateDefault());
        services.AddSingleton<ISlashCommandExecutor, SlashCommandExecutor>();
        services.AddSingleton<GuidedConversationComponent>();
        services.AddSingleton<ITuiApplication, StubTuiApplication>();
        services.AddSingleton<IComponentGallery>(sp =>
        {
            var gallery = new ComponentGallery();
            // Self-register all built-in components
            gallery.Register(sp.GetRequiredService<TopPanelComponent>());
            gallery.Register(sp.GetRequiredService<ContextPanelComponent>());
            gallery.Register(sp.GetRequiredService<ActivityPanelComponent>());
            gallery.Register(sp.GetRequiredService<PromptAreaComponent>());
            gallery.Register(new LandingPageComponent());
            gallery.Register(new SessionResumeModalComponent());
            gallery.Register(new DiffViewerComponent());
            gallery.Register(new PhaseTransitionComponent());
            gallery.Register(new ResearchDisplayComponent());
            gallery.Register(new FilePickerComponent());
            gallery.Register(new SelectionModalComponent());
            gallery.Register(new ConfirmationModalComponent());
            gallery.Register(new ErrorModalComponent());
            gallery.Register(new SpinnerComponent());
            gallery.Register(sp.GetRequiredService<GuidedConversationComponent>());
            return gallery;
        });
        return services;
    }

    /// <summary>
    /// Replaces the stub TUI with the real TuiApplication that runs a
    /// full-screen Spectre.Tui render loop. Call after <see cref="AddLopenTui"/>
    /// and only in the real CLI entry point (not in tests or headless mode).
    /// </summary>
    public static IServiceCollection UseRealTui(this IServiceCollection services)
    {
        // Remove the stub registration and replace with real TUI
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(ITuiApplication));
        if (descriptor is not null)
            services.Remove(descriptor);

        services.AddSingleton<TuiApplication>();
        services.AddSingleton<ITuiApplication>(sp => sp.GetRequiredService<TuiApplication>());
        return services;
    }

    /// <summary>
    /// Registers <see cref="TopPanelDataProvider"/> to supply live data to the top panel.
    /// Requires ITokenTracker, IGitService, IAuthService, IWorkflowEngine, and IModelSelector
    /// to be registered. Call after all module registrations.
    /// </summary>
    public static IServiceCollection AddTopPanelDataProvider(this IServiceCollection services)
    {
        services.AddSingleton<ITopPanelDataProvider, TopPanelDataProvider>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="ContextPanelDataProvider"/> to supply live data to the context panel.
    /// Requires IPlanManager and IWorkflowEngine to be registered.
    /// Optionally uses IResourceTracker when available to populate active resources.
    /// Call after all module registrations.
    /// </summary>
    public static IServiceCollection AddContextPanelDataProvider(this IServiceCollection services)
    {
        services.AddSingleton<IContextPanelDataProvider>(sp =>
            new ContextPanelDataProvider(
                sp.GetRequiredService<Lopen.Storage.IPlanManager>(),
                sp.GetRequiredService<Lopen.Core.Workflow.IWorkflowEngine>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ContextPanelDataProvider>>(),
                sp.GetService<Lopen.Core.Documents.IResourceTracker>()));
        return services;
    }

    /// <summary>
    /// Registers <see cref="ActivityPanelDataProvider"/> to supply live data to the activity panel.
    /// Call after all module registrations.
    /// </summary>
    public static IServiceCollection AddActivityPanelDataProvider(this IServiceCollection services)
    {
        services.AddSingleton<IActivityPanelDataProvider, ActivityPanelDataProvider>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="UserPromptQueue"/> as a singleton for passing user prompts
    /// from the TUI to the orchestrator or command handler.
    /// </summary>
    public static IServiceCollection AddUserPromptQueue(this IServiceCollection services)
    {
        services.AddSingleton<IUserPromptQueue, UserPromptQueue>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="TuiOutputRenderer"/> as the <see cref="IOutputRenderer"/> for TUI mode,
    /// replacing the default <see cref="HeadlessRenderer"/>. Call after registering activity panel
    /// data provider and user prompt queue.
    /// </summary>
    public static IServiceCollection AddTuiOutputRenderer(this IServiceCollection services)
    {
        // Remove any existing IOutputRenderer registration (e.g. HeadlessRenderer)
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IOutputRenderer));
        if (descriptor is not null)
            services.Remove(descriptor);

        services.AddSingleton<IOutputRenderer>(sp =>
            new TuiOutputRenderer(
                sp.GetRequiredService<IActivityPanelDataProvider>(),
                sp.GetService<IUserPromptQueue>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TuiOutputRenderer>>()));
        return services;
    }

    /// <summary>
    /// Registers <see cref="SessionDetector"/> to detect active sessions for the resume modal.
    /// Requires ISessionManager to be registered.
    /// </summary>
    public static IServiceCollection AddSessionDetector(this IServiceCollection services)
    {
        services.AddSingleton<ISessionDetector, SessionDetector>();
        return services;
    }
}
