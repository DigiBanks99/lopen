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
            gallery.Register(new TopPanelComponent());
            gallery.Register(new ContextPanelComponent());
            gallery.Register(new ActivityPanelComponent());
            gallery.Register(new PromptAreaComponent());
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
            gallery.Register(new GuidedConversationComponent());
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
    /// Call after all module registrations.
    /// </summary>
    public static IServiceCollection AddContextPanelDataProvider(this IServiceCollection services)
    {
        services.AddSingleton<IContextPanelDataProvider, ContextPanelDataProvider>();
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
    /// Registers <see cref="SessionDetector"/> to detect active sessions for the resume modal.
    /// Requires ISessionManager to be registered.
    /// </summary>
    public static IServiceCollection AddSessionDetector(this IServiceCollection services)
    {
        services.AddSingleton<ISessionDetector, SessionDetector>();
        return services;
    }
}
