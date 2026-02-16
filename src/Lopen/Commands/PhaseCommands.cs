using System.CommandLine;
using Lopen.Core.Workflow;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Commands;

/// <summary>
/// Defines the phase subcommands: spec, plan, and build.
/// Each scopes Lopen to a specific workflow phase.
/// </summary>
public static class PhaseCommands
{
    public static Command CreateSpec(IServiceProvider services, TextWriter? output = null, TextWriter? error = null)
    {
        var stdout = output ?? Console.Out;
        var stderr = error ?? Console.Error;

        var spec = new Command("spec", "Run the Requirement Gathering phase (step 1)");
        spec.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            try
            {
                var headlessError = await ValidateHeadlessPromptAsync(services, parseResult, stderr, cancellationToken);
                if (headlessError is not null)
                    return headlessError.Value;

                var (sessionId, resolveError) = await ResolveSessionAsync(services, parseResult, cancellationToken);
                if (resolveError is not null)
                {
                    await stderr.WriteLineAsync(resolveError);
                    return ExitCodes.Failure;
                }

                if (sessionId is not null)
                {
                    await stdout.WriteLineAsync($"Resuming session: {sessionId}");
                }

                await stdout.WriteLineAsync("Starting requirement gathering phase...");

                var orchestrator = services.GetService<IWorkflowOrchestrator>();
                var module = await ResolveModuleNameAsync(services, sessionId, cancellationToken);

                if (orchestrator is not null && module is not null)
                {
                    var result = await orchestrator.RunAsync(module, cancellationToken);
                    if (!result.IsComplete && result.WasInterrupted)
                    {
                        await stdout.WriteLineAsync(result.Summary ?? "Requirement gathering paused.");
                    }
                }

                return ExitCodes.Success;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return ExitCodes.Failure;
            }
        });

        return spec;
    }

    public static Command CreatePlan(IServiceProvider services, TextWriter? output = null, TextWriter? error = null)
    {
        var stdout = output ?? Console.Out;
        var stderr = error ?? Console.Error;

        var plan = new Command("plan", "Run the Planning phase (steps 2–5)");
        plan.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            try
            {
                var headlessError = await ValidateHeadlessPromptAsync(services, parseResult, stderr, cancellationToken);
                if (headlessError is not null)
                    return headlessError.Value;

                var (sessionId, resolveError) = await ResolveSessionAsync(services, parseResult, cancellationToken);
                if (resolveError is not null)
                {
                    await stderr.WriteLineAsync(resolveError);
                    return ExitCodes.Failure;
                }

                var validationResult = await ValidateSpecExistsAsync(services, cancellationToken);
                if (validationResult is not null)
                {
                    await stderr.WriteLineAsync(validationResult);
                    return ExitCodes.Failure;
                }

                if (sessionId is not null)
                {
                    await stdout.WriteLineAsync($"Resuming session: {sessionId}");
                }

                await stdout.WriteLineAsync("Starting planning phase...");

                var orchestrator = services.GetService<IWorkflowOrchestrator>();
                var module = await ResolveModuleNameAsync(services, sessionId, cancellationToken);

                if (orchestrator is not null && module is not null)
                {
                    var result = await orchestrator.RunAsync(module, cancellationToken);
                    if (!result.IsComplete && result.WasInterrupted)
                    {
                        await stdout.WriteLineAsync(result.Summary ?? "Planning paused.");
                    }
                }

                return ExitCodes.Success;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return ExitCodes.Failure;
            }
        });

        return plan;
    }

    public static Command CreateBuild(IServiceProvider services, TextWriter? output = null, TextWriter? error = null)
    {
        var stdout = output ?? Console.Out;
        var stderr = error ?? Console.Error;

        var build = new Command("build", "Run the Building phase (steps 6–7)");
        build.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            try
            {
                var headlessError = await ValidateHeadlessPromptAsync(services, parseResult, stderr, cancellationToken);
                if (headlessError is not null)
                    return headlessError.Value;

                var (sessionId, resolveError) = await ResolveSessionAsync(services, parseResult, cancellationToken);
                if (resolveError is not null)
                {
                    await stderr.WriteLineAsync(resolveError);
                    return ExitCodes.Failure;
                }

                var specResult = await ValidateSpecExistsAsync(services, cancellationToken);
                if (specResult is not null)
                {
                    await stderr.WriteLineAsync(specResult);
                    return ExitCodes.Failure;
                }

                var planResult = await ValidatePlanExistsAsync(services, cancellationToken);
                if (planResult is not null)
                {
                    await stderr.WriteLineAsync(planResult);
                    return ExitCodes.Failure;
                }

                if (sessionId is not null)
                {
                    await stdout.WriteLineAsync($"Resuming session: {sessionId}");
                }

                await stdout.WriteLineAsync("Starting building phase...");

                var orchestrator = services.GetService<IWorkflowOrchestrator>();
                var module = await ResolveModuleNameAsync(services, sessionId, cancellationToken);

                if (orchestrator is not null && module is not null)
                {
                    var result = await orchestrator.RunAsync(module, cancellationToken);
                    if (!result.IsComplete && result.WasInterrupted)
                    {
                        await stdout.WriteLineAsync(result.Summary ?? "Building paused.");
                    }
                }

                return ExitCodes.Success;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return ExitCodes.Failure;
            }
        });

        return build;
    }

    /// <summary>
    /// In headless mode, validates that either --prompt is provided or an active session exists.
    /// Returns an exit code if validation fails, null if valid.
    /// </summary>
    internal static async Task<int?> ValidateHeadlessPromptAsync(
        IServiceProvider services, ParseResult parseResult, TextWriter stderr, CancellationToken cancellationToken)
    {
        var headless = parseResult.GetValue(GlobalOptions.Headless);
        var prompt = parseResult.GetValue(GlobalOptions.Prompt);

        if (!headless || !string.IsNullOrWhiteSpace(prompt))
            return null;

        var sessionManager = services.GetService<ISessionManager>();
        if (sessionManager is null)
        {
            await stderr.WriteLineAsync("Headless mode requires --prompt or an active session. Run with --prompt <text> or start a session first.");
            return ExitCodes.Failure;
        }
        var latestId = await sessionManager.GetLatestSessionIdAsync(cancellationToken);
        if (latestId is null)
        {
            await stderr.WriteLineAsync("Headless mode requires --prompt or an active session. Run with --prompt <text> or start a session first.");
            return ExitCodes.Failure;
        }

        return null;
    }

    /// <summary>
    /// Validates that a specification exists for the current module.
    /// Returns an error message if validation fails, null if valid.
    /// </summary>
    internal static async Task<string?> ValidateSpecExistsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var sessionManager = services.GetRequiredService<ISessionManager>();
        var moduleScanner = services.GetRequiredService<IModuleScanner>();

        var latestId = await sessionManager.GetLatestSessionIdAsync(cancellationToken);
        if (latestId is null)
        {
            return "No active session found. Run 'lopen spec' first to create a specification.";
        }

        var state = await sessionManager.LoadSessionStateAsync(latestId, cancellationToken);
        if (state is null)
        {
            return $"Session state not found: {latestId}";
        }

        var modules = moduleScanner.ScanModules();
        var moduleInfo = modules.FirstOrDefault(m =>
            string.Equals(m.Name, state.Module, StringComparison.OrdinalIgnoreCase));

        if (moduleInfo is null || !moduleInfo.HasSpecification)
        {
            return $"No specification found for module '{state.Module}'. Run 'lopen spec' first.";
        }

        return null;
    }

    /// <summary>
    /// Validates that a plan exists for the current module.
    /// Returns an error message if validation fails, null if valid.
    /// </summary>
    internal static async Task<string?> ValidatePlanExistsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var sessionManager = services.GetRequiredService<ISessionManager>();
        var planManager = services.GetRequiredService<IPlanManager>();

        var latestId = await sessionManager.GetLatestSessionIdAsync(cancellationToken);
        if (latestId is null)
        {
            return "No active session found. Run 'lopen plan' first to create a plan.";
        }

        var state = await sessionManager.LoadSessionStateAsync(latestId, cancellationToken);
        if (state is null)
        {
            return $"Session state not found: {latestId}";
        }

        var planExists = await planManager.PlanExistsAsync(state.Module, cancellationToken);
        if (!planExists)
        {
            return $"No plan found for module '{state.Module}'. Run 'lopen plan' first.";
        }

        return null;
    }

    /// <summary>
    /// Resolves which session to use based on --resume/--no-resume flags.
    /// Returns (sessionId, errorMessage). If sessionId is null and errorMessage is not null, an error occurred.
    /// If both are null, no session and --no-resume was specified (start fresh).
    /// </summary>
    internal static async Task<(SessionId? sessionId, string? errorMessage)> ResolveSessionAsync(
        IServiceProvider services, ParseResult parseResult, CancellationToken cancellationToken)
    {
        var resumeId = parseResult.GetValue(GlobalOptions.Resume);
        var noResume = parseResult.GetValue(GlobalOptions.NoResume);

        if (noResume)
        {
            return (null, null);
        }

        var sessionManager = services.GetService<ISessionManager>();
        if (sessionManager is null)
        {
            // No session manager available (e.g., no project root). Start fresh.
            return (null, null);
        }

        if (!string.IsNullOrWhiteSpace(resumeId))
        {
            var parsed = SessionId.TryParse(resumeId);
            if (parsed is null)
            {
                return (null, $"Invalid session ID format: '{resumeId}'. Expected format: <module>-YYYYMMDD-<counter>.");
            }

            var state = await sessionManager.LoadSessionStateAsync(parsed, cancellationToken);
            if (state is null)
            {
                return (null, $"Session not found: '{resumeId}'.");
            }

            if (string.Equals(state.Phase, "complete", StringComparison.OrdinalIgnoreCase))
            {
                return (null, $"Session '{resumeId}' is already complete. Use --no-resume to start fresh.");
            }

            await sessionManager.SetLatestAsync(parsed, cancellationToken);
            return (parsed, null);
        }

        // No explicit flags: check for latest active session
        var latestId = await sessionManager.GetLatestSessionIdAsync(cancellationToken);
        if (latestId is not null)
        {
            var state = await sessionManager.LoadSessionStateAsync(latestId, cancellationToken);
            if (state is not null && !string.Equals(state.Phase, "complete", StringComparison.OrdinalIgnoreCase))
            {
                return (latestId, null);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Resolves the module name from the session state.
    /// </summary>
    internal static async Task<string?> ResolveModuleNameAsync(
        IServiceProvider services, SessionId? sessionId, CancellationToken cancellationToken)
    {
        if (sessionId is null)
            return null;

        var sessionManager = services.GetService<ISessionManager>();
        if (sessionManager is null)
            return null;

        var state = await sessionManager.LoadSessionStateAsync(sessionId, cancellationToken);
        return state?.Module;
    }
}
