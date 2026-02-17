using Lopen.Configuration;
using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Lopen.Core.ToolHandlers;
using Lopen.Core.Workflow;
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
            services.AddSingleton<IStateAssessor, CodebaseStateAssessor>();
            services.AddSingleton<IWorkflowEngine, WorkflowEngine>();
            services.AddSingleton<IFailureHandler>(sp =>
            {
                var workflowOptions = sp.GetService<WorkflowOptions>();
                var threshold = workflowOptions?.FailureThreshold ?? 3;
                return new FailureHandler(
                    sp.GetRequiredService<ILogger<FailureHandler>>(),
                    threshold);
            });
            services.AddSingleton<IWorkflowOrchestrator>(sp =>
            {
                Git.IGitWorkflowService? gitService = null;
                try
                { gitService = sp.GetService<Git.IGitWorkflowService>(); }
                catch { /* Git service optional — dependencies may not be registered */ }

                Lopen.Storage.IAutoSaveService? autoSave = null;
                try
                { autoSave = sp.GetService<Lopen.Storage.IAutoSaveService>(); }
                catch { /* Auto-save optional */ }

                Lopen.Storage.ISessionManager? sessionMgr = null;
                try
                { sessionMgr = sp.GetService<Lopen.Storage.ISessionManager>(); }
                catch { /* Session manager optional */ }

                Lopen.Llm.ITokenTracker? tokenTracker = null;
                try
                { tokenTracker = sp.GetService<Lopen.Llm.ITokenTracker>(); }
                catch { /* Token tracker optional */ }

                IFailureHandler? failureHandler = null;
                try
                { failureHandler = sp.GetService<IFailureHandler>(); }
                catch { /* Failure handler optional */ }

                Lopen.Configuration.IBudgetEnforcer? budgetEnforcer = null;
                try
                { budgetEnforcer = sp.GetService<Lopen.Configuration.IBudgetEnforcer>(); }
                catch { /* Budget enforcer optional */ }

                Lopen.Storage.IPlanManager? planMgr = null;
                try
                { planMgr = sp.GetService<Lopen.Storage.IPlanManager>(); }
                catch { /* Plan manager optional */ }

                IPauseController? pauseCtrl = null;
                try
                { pauseCtrl = sp.GetService<IPauseController>(); }
                catch { /* Pause controller optional */ }

                return new WorkflowOrchestrator(
                    sp.GetRequiredService<IWorkflowEngine>(),
                    sp.GetRequiredService<IStateAssessor>(),
                    sp.GetRequiredService<Lopen.Llm.ILlmService>(),
                    sp.GetRequiredService<Lopen.Llm.IPromptBuilder>(),
                    sp.GetRequiredService<Lopen.Llm.IToolRegistry>(),
                    sp.GetRequiredService<Lopen.Llm.IModelSelector>(),
                    sp.GetRequiredService<BackPressure.IGuardrailPipeline>(),
                    sp.GetRequiredService<IOutputRenderer>(),
                    sp.GetRequiredService<IPhaseTransitionController>(),
                    sp.GetRequiredService<ISpecificationDriftService>(),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<WorkflowOrchestrator>>(),
                    gitService,
                    autoSave,
                    sessionMgr,
                    tokenTracker,
                    failureHandler,
                    budgetEnforcer,
                    planMgr,
                    pauseCtrl,
                    sp.GetService<IUserPromptQueue>(),
                    sp.GetService<WorkflowOptions>());
            });
            services.AddSingleton<IPauseController, PauseController>();
            services.AddSingleton<ISpecificationDriftService, SpecificationDriftService>();
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
