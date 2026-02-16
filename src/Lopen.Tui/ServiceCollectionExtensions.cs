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
            return gallery;
        });
        return services;
    }
}
