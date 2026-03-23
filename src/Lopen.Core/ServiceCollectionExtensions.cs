using Lopen.Configuration;
using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Lopen.Core.ToolHandlers;
using Lopen.Core.Workflow;
using Lopen.Llm;
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
            services.AddSingleton<IToolHandlerBinder>(sp =>
            {
                Git.IGitWorkflowService? gitSvc = null;
                try
                { gitSvc = sp.GetService<Git.IGitWorkflowService>(); }
                catch { /* Git service optional */ }

                Lopen.Llm.ITaskStatusGate? taskGate = null;
                try
                { taskGate = sp.GetService<Lopen.Llm.ITaskStatusGate>(); }
                catch { /* Task status gate optional */ }

                Lopen.Storage.IPlanManager? planMgr = null;
                try
                { planMgr = sp.GetService<Lopen.Storage.IPlanManager>(); }
                catch { /* Plan manager optional */ }

                Lopen.Llm.IOracleVerifier? oracleVerifier = null;
                try
                { oracleVerifier = sp.GetService<Lopen.Llm.IOracleVerifier>(); }
                catch { /* Oracle verifier optional */ }

                return new ToolHandlerBinder(
                    sp.GetRequiredService<Lopen.Storage.IFileSystem>(),
                    sp.GetRequiredService<ISectionExtractor>(),
                    sp.GetRequiredService<IWorkflowEngine>(),
                    sp.GetRequiredService<Lopen.Llm.IVerificationTracker>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ToolHandlerBinder>>(),
                    projectRoot,
                    gitSvc,
                    taskGate,
                    planMgr,
                    oracleVerifier);
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
