using System.CommandLine;
using System.Diagnostics;
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
        TextWriter stdout = output ?? Console.Out;
        TextWriter stderr = error ?? Console.Error;

        return rootCommand =>
        {
            rootCommand.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
            {
                bool headless = parseResult.GetValue(GlobalOptions.Headless);

                // OTEL-01: Root command span
                using Activity? activity = SpanFactory.StartCommand("lopen", headless);
                var sw = Stopwatch.StartNew();
                LopenTelemetryDiagnostics.CommandCount.Add(1, new KeyValuePair<string, object?>("lopen.command.name", "lopen"));

                try
                {
                    // Apply --model, --unattended, --max-iterations overrides (CFG-08, CFG-09, CFG-11)
                    GlobalOptions.ApplyConfigOverrides(services, parseResult);

                    int exitCode;
                    if (headless)
                    {
                        int? headlessError = await PhaseCommands.ValidateHeadlessPromptAsync(
                            services, parseResult, stderr, cancellationToken);
                        if (headlessError is not null)
                        {
                            SpanFactory.SetCommandExitCode(activity, headlessError.Value);
                            return headlessError.Value;
                        }

                        string? authError = await PhaseCommands.ValidateAuthAsync(services, cancellationToken);
                        if (authError is not null)
                        {
                            await stderr.WriteLineAsync(authError);
                            SpanFactory.SetCommandExitCode(activity, ExitCodes.Failure);
                            return ExitCodes.Failure;
                        }

                        exitCode = await RunHeadlessAsync(services, parseResult, stdout, stderr, cancellationToken);
                    }
                    else
                    {
                        string? authError = await PhaseCommands.ValidateAuthAsync(services, cancellationToken);
                        if (authError is not null)
                        {
                            await stderr.WriteLineAsync(authError);
                            SpanFactory.SetCommandExitCode(activity, ExitCodes.Failure);
                            return ExitCodes.Failure;
                        }

                        (SessionId? sessionId, string? resolveError) = await PhaseCommands.ResolveSessionAsync(
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

                        ITuiApplication app = services.GetRequiredService<ITuiApplication>();
                        string? prompt = parseResult.GetValue(GlobalOptions.Prompt);
                        if (parseResult.GetValue(GlobalOptions.NoWelcome))
                        {
                            app.SuppressLandingPage();
                        }
                        string? resumeId = parseResult.GetValue(GlobalOptions.Resume);
                        if (!string.IsNullOrEmpty(resumeId))
                        {
                            app.SuppressSessionResumeModal();
                        }
                        await app.RunAsync(prompt, cancellationToken);
                        exitCode = ExitCodes.Success;
                    }

                    SpanFactory.SetCommandExitCode(activity, exitCode);
                    LopenTelemetryDiagnostics.CommandDuration.Record(
                        sw.Elapsed.TotalMilliseconds,
                        new KeyValuePair<string, object?>("lopen.command.name", "lopen"));
                    return exitCode;
                }
                catch (Exception ex)
                {
                    LopenTelemetryDiagnostics.CommandDuration.Record(
                        sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("lopen.command.name", "lopen"));
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
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
        (SessionId? sessionId, string? resolveError) = await PhaseCommands.ResolveSessionAsync(
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

        IWorkflowOrchestrator? orchestrator = services.GetService<IWorkflowOrchestrator>();
        if (orchestrator is null)
        {
            await stderr.WriteLineAsync("Workflow engine not available. Ensure project root is configured.");
            return ExitCodes.Failure;
        }

        string? module = await PhaseCommands.ResolveModuleNameAsync(services, sessionId, cancellationToken);
        if (module is null)
        {
            await stderr.WriteLineAsync("No module specified. Create or resume a session first.");
            return ExitCodes.Failure;
        }

        string? prompt = parseResult.GetValue(GlobalOptions.Prompt);

        await stdout.WriteLineAsync($"Running headless workflow for module: {module}");
        OrchestrationResult result = await orchestrator.RunAsync(module, prompt, cancellationToken);

        if (result.IsComplete)
        {
            await stdout.WriteLineAsync($"Module '{module}' completed after {result.IterationCount} iterations.");
            return ExitCodes.Success;
        }

        if (result.WasInterrupted)
        {
            await stderr.WriteLineAsync(result.Summary ?? "Workflow interrupted. User intervention may be required.");
            return ExitCodes.UserInterventionRequired;
        }

        return ExitCodes.Success;
    }
}
