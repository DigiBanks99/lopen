using Lopen.Core.Git;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;

namespace Lopen.Commands;

/// <summary>
/// Defines the 'revert' command that rolls back to the last task-completion commit.
/// </summary>
public static class RevertCommand
{
    public static Command Create(IServiceProvider services, TextWriter? output = null, TextWriter? error = null)
    {
        TextWriter stdout = output ?? Console.Out;
        TextWriter stderr = error ?? Console.Error;

        var revert = new Command("revert", "Roll back to the last task-completion commit");
        revert.SetAction(async (ParseResult _, CancellationToken cancellationToken) =>
        {
            try
            {
                IRevertService? revertService = services.GetService<IRevertService>();
                if (revertService is null)
                {
                    await stderr.WriteLineAsync("No project found. Run from a directory with a .lopen/ or .git/ folder.");
                    return 1;
                }

                ISessionManager? sessionManager = services.GetService<ISessionManager>();
                if (sessionManager is null)
                {
                    await stderr.WriteLineAsync("No project found. Run from a directory with a .lopen/ or .git/ folder.");
                    return 1;
                }
                SessionId? latestId = await sessionManager.GetLatestSessionIdAsync(cancellationToken);
                if (latestId is null)
                {
                    await stderr.WriteLineAsync("No active session found.");
                    return 1;
                }

                SessionState? state = await sessionManager.LoadSessionStateAsync(latestId, cancellationToken);
                if (state is null)
                {
                    await stderr.WriteLineAsync($"Session state not found: {latestId}");
                    return 1;
                }

                if (string.IsNullOrWhiteSpace(state.LastTaskCompletionCommitSha))
                {
                    await stderr.WriteLineAsync("No task-completion commits found in current session.");
                    return 1;
                }

                RevertResult result = await revertService.RevertToCommitAsync(state.LastTaskCompletionCommitSha, cancellationToken);

                if (result.Success)
                {
                    // Update session state to reflect the rollback
                    SessionState updatedState = state with
                    {
                        LastTaskCompletionCommitSha = null,
                        UpdatedAt = DateTimeOffset.UtcNow,
                    };
                    await sessionManager.SaveSessionStateAsync(latestId, updatedState, cancellationToken);

                    await stdout.WriteLineAsync($"Reverted to commit {result.RevertedToCommitSha}.");
                    await stdout.WriteLineAsync(result.Message);
                    return 0;
                }
                else
                {
                    await stderr.WriteLineAsync($"Revert failed: {result.Message}");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });

        return revert;
    }
}
