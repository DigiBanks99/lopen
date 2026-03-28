using Lopen.Configuration;
using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Lopen.Llm.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
            services.AddSingleton<IModuleSelectionService, ModuleSelectionService>();
            services.AddSingleton<CodebaseStateAssessor>(sp =>
                new CodebaseStateAssessor(
                    sp.GetRequiredService<Lopen.Storage.IFileSystem>(),
                    sp.GetRequiredService<IModuleScanner>(),
                    projectRoot,
                    sp.GetRequiredService<ILogger<CodebaseStateAssessor>>()));
            services.AddSingleton<IStateAssessor>(sp => sp.GetRequiredService<CodebaseStateAssessor>());
            services.AddSingleton<IStepRecorder>(sp => sp.GetRequiredService<CodebaseStateAssessor>());
            services.AddSingleton<IWorkflowEngine, WorkflowEngine>();
            services.AddSingleton<IFailureHandler>(sp =>
            {
                WorkflowOptions? workflowOptions = sp.GetService<WorkflowOptions>();
                var threshold = workflowOptions?.FailureThreshold ?? 3;
                return new FailureHandler(
                    sp.GetRequiredService<ILogger<FailureHandler>>(),
                    threshold);
            });
            services.AddSingleton<IWorkflowOrchestrator, WorkflowOrchestrator>();
            services.AddSingleton<IPauseController, PauseController>();
            services.AddSingleton<ISpecificationDriftService, SpecificationDriftService>();
            services.AddSingleton<IResourceTracker>(sp =>
                new ResourceTracker(
                    sp.GetRequiredService<Lopen.Storage.IFileSystem>(),
                    projectRoot,
                    sp.GetRequiredService<ILogger<ResourceTracker>>()));

            services.AddSingleton<ToolCatalog>(sp =>
            {
                var fs = sp.GetRequiredService<Lopen.Storage.IFileSystem>();
                var sectionExtractor = sp.GetRequiredService<ISectionExtractor>();
                var engine = sp.GetRequiredService<IWorkflowEngine>();
                var verificationTracker = sp.GetRequiredService<Lopen.Llm.IVerificationTracker>();

                Lopen.Llm.ITaskStatusGate? taskGateCatalog = null;
                try { taskGateCatalog = sp.GetService<Lopen.Llm.ITaskStatusGate>(); }
                catch { /* optional */ }

                Lopen.Storage.IPlanManager? planMgrCatalog = null;
                try { planMgrCatalog = sp.GetService<Lopen.Storage.IPlanManager>(); }
                catch { /* optional */ }

                Lopen.Llm.IOracleVerifier? oracleCatalog = null;
                try { oracleCatalog = sp.GetService<Lopen.Llm.IOracleVerifier>(); }
                catch { /* optional */ }

                var toolSectionExtractor = new SectionExtractorToolAdapter(sectionExtractor);
                var toolWorkflowEngine = new WorkflowEngineToolAdapter(engine);

                return new ToolCatalog(
                    fs,
                    toolSectionExtractor,
                    toolWorkflowEngine,
                    verificationTracker,
                    projectRoot,
                    taskGateCatalog,
                    planMgrCatalog,
                    oracleCatalog);
            });
        }

        services.AddSingleton<IPhaseTransitionController, PhaseTransitionController>();
        services.AddSingleton<ISpecificationParser, MarkdigSpecificationParser>();
        services.AddSingleton<IContentHasher, XxHashContentHasher>();
        services.AddSingleton<IDriftDetector, DriftDetector>();
        services.AddSingleton<ISectionExtractor, SectionExtractor>();

        // Register guardrails
        services.AddSingleton<IGuardrail>(sp =>
        {
            ToolDisciplineOptions? options = sp.GetService<ToolDisciplineOptions>();
            return options is not null
                ? new ToolDisciplineGuardrail(options)
                : new ToolDisciplineGuardrail();
        });
        services.AddSingleton<IGuardrail>(sp =>
        {
            IVerificationTracker? tracker = sp.GetService<Lopen.Llm.IVerificationTracker>();
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

        // Register ResourceLimitGuardrail (CORE-11) when token tracker and budget are available
        services.AddSingleton<IGuardrail>(sp =>
        {
            ITokenTracker? tokenTracker = sp.GetService<Lopen.Llm.ITokenTracker>();
            BudgetOptions? budgetOptions = sp.GetService<BudgetOptions>();
            var budget = budgetOptions?.PremiumRequestBudget ?? 0;
            if (tokenTracker is not null && budget > 0)
            {
                return new ResourceLimitGuardrail(
                    tokenTracker,
                    sp.GetRequiredService<ILogger<ResourceLimitGuardrail>>(),
                    budget);
            }
            // Return a pass-through guardrail when budget is not configured
            return new PassThroughGuardrail(order: 100);
        });

        // Register ChurnDetectionGuardrail (CORE-12)
        services.AddSingleton<IGuardrail>(sp =>
        {
            WorkflowOptions? workflowOptions = sp.GetService<WorkflowOptions>();
            var threshold = workflowOptions?.FailureThreshold ?? 3;
            return new ChurnDetectionGuardrail(threshold);
        });

        services.AddSingleton<IGuardrailPipeline, GuardrailPipeline>();

        // Default to headless renderer; CLI overrides with TUI renderer when appropriate
        services.TryAddSingleton<IOutputRenderer>(new HeadlessRenderer());

        return services;
    }
}
