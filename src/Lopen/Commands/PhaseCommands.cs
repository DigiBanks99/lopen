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
        spec.SetAction(async (ParseResult _, CancellationToken cancellationToken) =>
        {
            try
            {
                await stdout.WriteLineAsync("Starting requirement gathering phase...");
                await stdout.WriteLineAsync("Workflow engine not yet wired to CLI. Use the TUI for interactive spec gathering.");
                return 0;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });

        return spec;
    }

    public static Command CreatePlan(IServiceProvider services, TextWriter? output = null, TextWriter? error = null)
    {
        var stdout = output ?? Console.Out;
        var stderr = error ?? Console.Error;

        var plan = new Command("plan", "Run the Planning phase (steps 2–5)");
        plan.SetAction(async (ParseResult _, CancellationToken cancellationToken) =>
        {
            try
            {
                var validationResult = await ValidateSpecExistsAsync(services, cancellationToken);
                if (validationResult is not null)
                {
                    await stderr.WriteLineAsync(validationResult);
                    return 1;
                }

                await stdout.WriteLineAsync("Starting planning phase...");
                await stdout.WriteLineAsync("Workflow engine not yet wired to CLI. Use the TUI for interactive planning.");
                return 0;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });

        return plan;
    }

    public static Command CreateBuild(IServiceProvider services, TextWriter? output = null, TextWriter? error = null)
    {
        var stdout = output ?? Console.Out;
        var stderr = error ?? Console.Error;

        var build = new Command("build", "Run the Building phase (steps 6–7)");
        build.SetAction(async (ParseResult _, CancellationToken cancellationToken) =>
        {
            try
            {
                var specResult = await ValidateSpecExistsAsync(services, cancellationToken);
                if (specResult is not null)
                {
                    await stderr.WriteLineAsync(specResult);
                    return 1;
                }

                var planResult = await ValidatePlanExistsAsync(services, cancellationToken);
                if (planResult is not null)
                {
                    await stderr.WriteLineAsync(planResult);
                    return 1;
                }

                await stdout.WriteLineAsync("Starting building phase...");
                await stdout.WriteLineAsync("Workflow engine not yet wired to CLI. Use the TUI for interactive building.");
                return 0;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });

        return build;
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
}
