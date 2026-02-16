using Lopen.Configuration;
using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Lopen.Core.ToolHandlers;
using Lopen.Core.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Lopen.Core;

/// <summary>
/// Extension methods for registering Core module services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Core module services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="projectRoot">The project root directory for git and module scanning.</param>
    public static IServiceCollection AddLopenCore(this IServiceCollection services, string? projectRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            services.AddSingleton<IGitService>(sp =>
                new GitCliService(
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GitCliService>>(),
                    projectRoot));
            services.AddSingleton<IGitWorkflowService, GitWorkflowService>();
            services.AddSingleton<IRevertService, RevertService>();
            services.AddSingleton<IModuleScanner>(sp =>
                new ModuleScanner(
                    sp.GetRequiredService<Lopen.Storage.IFileSystem>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ModuleScanner>>(),
                    projectRoot));
            services.AddSingleton<IModuleLister, ModuleLister>();
            services.AddSingleton<IStateAssessor, CodebaseStateAssessor>();
            services.AddSingleton<IWorkflowEngine, WorkflowEngine>();
            services.AddSingleton<IWorkflowOrchestrator, WorkflowOrchestrator>();
            services.AddSingleton<IToolHandlerBinder>(sp =>
                new ToolHandlerBinder(
                    sp.GetRequiredService<Lopen.Storage.IFileSystem>(),
                    sp.GetRequiredService<ISectionExtractor>(),
                    sp.GetRequiredService<IWorkflowEngine>(),
                    sp.GetRequiredService<Lopen.Llm.IVerificationTracker>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ToolHandlerBinder>>(),
                    projectRoot));
        }

        services.AddSingleton<IPhaseTransitionController, PhaseTransitionController>();
        services.AddSingleton<ISpecificationParser, MarkdigSpecificationParser>();
        services.AddSingleton<IContentHasher, XxHashContentHasher>();
        services.AddSingleton<IDriftDetector, DriftDetector>();
        services.AddSingleton<ISectionExtractor, SectionExtractor>();

        // Register guardrails
        services.AddSingleton<IGuardrail>(sp =>
        {
            var options = sp.GetService<ToolDisciplineOptions>();
            return options is not null
                ? new ToolDisciplineGuardrail(options)
                : new ToolDisciplineGuardrail();
        });
        services.AddSingleton<IGuardrail>(sp =>
        {
            var tracker = sp.GetService<Lopen.Llm.IVerificationTracker>();
            if (tracker is null)
            {
                return new QualityGateGuardrail(
                    isCompletionBoundary: _ => false,
                    hasPassingVerification: _ => true);
            }

            return new QualityGateGuardrail(
                isCompletionBoundary: ctx => ctx.TaskName is not null,
                hasPassingVerification: ctx =>
                    ctx.TaskName is not null &&
                    tracker.IsVerified(Lopen.Llm.VerificationScope.Task, ctx.TaskName));
        });
        services.AddSingleton<IGuardrailPipeline, GuardrailPipeline>();

        // Default to headless renderer; CLI overrides with TUI renderer when appropriate
        services.TryAddSingleton<IOutputRenderer>(new HeadlessRenderer());

        return services;
    }
}
