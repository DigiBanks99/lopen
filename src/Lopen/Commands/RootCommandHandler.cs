using System.CommandLine;
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
                try
                {
                    var headless = parseResult.GetValue(GlobalOptions.Headless);

                    if (headless)
                    {
                        var headlessError = await PhaseCommands.ValidateHeadlessPromptAsync(
                            services, parseResult, stderr, cancellationToken);
                        if (headlessError is not null)
                            return headlessError.Value;
                    }

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

                    var app = services.GetRequiredService<ITuiApplication>();
                    await app.RunAsync(cancellationToken);

                    return ExitCodes.Success;
                }
                catch (Exception ex)
                {
                    await stderr.WriteLineAsync(ex.Message);
                    return ExitCodes.Failure;
                }
            });
        };
    }
}
