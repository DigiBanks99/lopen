using System.CommandLine;
using Lopen.Core.Workflow;
using Lopen.Otel;
using Lopen.Storage;
using Lopen.Tui;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Commands;

/// <summary>
/// Root command handler: launches the TUI with full workflow and session resume offer,
/// or runs headless if --headless is specified.
/// </summary>
public static class RootCommandHandler
{
    /// <summary>
    /// Creates the action for the root command (<c>lopen</c> with no subcommand).
    /// </summary>
    public static Action<RootCommand> Configure(IServiceProvider services, TextWriter? output = null, TextWriter? error = null)
    {
        var stdout = output ?? Console.Out;
        var stderr = error ?? Console.Error;

        return rootCommand =>
        {
            rootCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
            {
                var headless = parseResult.GetValue(GlobalOptions.Headless);

                // OTEL-01: Root command span
                using var activity = SpanFactory.StartCommand("lopen", headless);

                try
                {
                    int exitCode;
                    if (headless)
                    {
                        var headlessError = await PhaseCommands.ValidateHeadlessPromptAsync(
                            services, parseResult, stderr, cancellationToken);
                        if (headlessError is not null)
                        {
                            SpanFactory.SetCommandExitCode(activity, headlessError.Value);
                            return headlessError.Value;
                        }

                        exitCode = await RunHeadlessAsync(services, parseResult, stdout, stderr, cancellationToken);
                    }
                    else
                    {
                        var (sessionId, resolveError) = await PhaseCommands.ResolveSessionAsync(
                            services, parseResult, cancellationToken);
                        if (resolveError is not null)
                        {
                            await stderr.WriteLineAsync(resolveError);
                            SpanFactory.SetCommandExitCode(activity, ExitCodes.Failure);
                            return ExitCodes.Failure;
                        }

                        if (sessionId is not null)
                        {
                            await stdout.WriteLineAsync($"Resuming session: {sessionId}");
                        }

                        var app = services.GetRequiredService<ITuiApplication>();
                        await app.RunAsync(cancellationToken);
                        exitCode = ExitCodes.Success;
                    }

                    SpanFactory.SetCommandExitCode(activity, exitCode);
                    return exitCode;
                }
                catch (Exception ex)
                {
                    activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                    SpanFactory.SetCommandExitCode(activity, ExitCodes.Failure);
                    await stderr.WriteLineAsync(ex.Message);
                    return ExitCodes.Failure;
                }
            });
        };
    }

    /// <summary>
    /// Runs the full workflow autonomously in headless mode, writing plain text to stdout/stderr.
    /// </summary>
    internal static async Task<int> RunHeadlessAsync(
        IServiceProvider services, ParseResult parseResult,
        TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        var (sessionId, resolveError) = await PhaseCommands.ResolveSessionAsync(
            services, parseResult, cancellationToken);
        if (resolveError is not null)
        {
            await stderr.WriteLineAsync(resolveError);
            return ExitCodes.Failure;
        }

        if (sessionId is not null)
        {
            await stdout.WriteLineAsync($"Resuming session: {sessionId}");
        }

        var orchestrator = services.GetService<IWorkflowOrchestrator>();
        if (orchestrator is null)
        {
            await stderr.WriteLineAsync("Workflow engine not available. Ensure project root is configured.");
            return ExitCodes.Failure;
        }

        var module = await PhaseCommands.ResolveModuleNameAsync(services, sessionId, cancellationToken);
        if (module is null)
        {
            await stderr.WriteLineAsync("No module specified. Create or resume a session first.");
            return ExitCodes.Failure;
        }

        await stdout.WriteLineAsync($"Running headless workflow for module: {module}");
        var result = await orchestrator.RunAsync(module, cancellationToken);

        if (result.IsComplete)
        {
            await stdout.WriteLineAsync($"Module '{module}' completed after {result.IterationCount} iterations.");
            return ExitCodes.Success;
        }

        if (result.WasInterrupted)
        {
            await stderr.WriteLineAsync(result.Summary ?? "Workflow interrupted.");
            return ExitCodes.Failure;
        }

        return ExitCodes.Success;
    }
}
