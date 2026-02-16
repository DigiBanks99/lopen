using System.CommandLine;
using System.Text.Json;
using Lopen.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Commands;

/// <summary>
/// Defines the 'session' command group with list, show, resume, delete, and prune subcommands.
/// </summary>
public static class SessionCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static Command Create(IServiceProvider services, TextWriter? output = null, TextWriter? error = null)
    {
        var stdout = output ?? Console.Out;
        var stderr = error ?? Console.Error;

        var session = new Command("session", "Manage workflow sessions");

        session.Add(CreateListCommand(services, stdout, stderr));
        session.Add(CreateShowCommand(services, stdout, stderr));
        session.Add(CreateResumeCommand(services, stdout, stderr));
        session.Add(CreateDeleteCommand(services, stdout, stderr));
        session.Add(CreatePruneCommand(services, stdout, stderr));

        return session;
    }

    private static Command CreateListCommand(IServiceProvider services, TextWriter stdout, TextWriter stderr)
    {
        var list = new Command("list", "List all sessions");
        list.SetAction(async (ParseResult _, CancellationToken cancellationToken) =>
        {
            var sessionManager = services.GetRequiredService<ISessionManager>();
            try
            {
                var sessions = await sessionManager.ListSessionsAsync(cancellationToken);
                var latestId = await sessionManager.GetLatestSessionIdAsync(cancellationToken);

                if (sessions.Count == 0)
                {
                    await stdout.WriteLineAsync("No sessions found.");
                    return 0;
                }

                foreach (var id in sessions)
                {
                    var state = await sessionManager.LoadSessionStateAsync(id, cancellationToken);
                    var marker = id.Equals(latestId) ? " *" : "";
                    var status = state?.IsComplete == true ? "complete" : "active";
                    await stdout.WriteLineAsync($"{id}  [{status}]  {state?.Phase ?? "unknown"}/{state?.Step ?? "unknown"}{marker}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });
        return list;
    }

    private static Command CreateShowCommand(IServiceProvider services, TextWriter stdout, TextWriter stderr)
    {
        var show = new Command("show", "Show session details");
        var sessionIdArg = new Argument<string?>("session-id") { Description = "Session ID (defaults to latest)", Arity = ArgumentArity.ZeroOrOne };
        var formatOption = new Option<string>("--format") { Description = "Output format: md, json, yaml" };
        formatOption.DefaultValueFactory = _ => "md";

        show.Add(sessionIdArg);
        show.Add(formatOption);

        show.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var sessionManager = services.GetRequiredService<ISessionManager>();
            try
            {
                var sessionIdStr = parseResult.GetValue(sessionIdArg);
                var format = parseResult.GetValue(formatOption) ?? "md";

                SessionId? sessionId;
                if (string.IsNullOrWhiteSpace(sessionIdStr))
                {
                    sessionId = await sessionManager.GetLatestSessionIdAsync(cancellationToken);
                    if (sessionId is null)
                    {
                        await stderr.WriteLineAsync("No active session found.");
                        return 1;
                    }
                }
                else
                {
                    sessionId = SessionId.TryParse(sessionIdStr);
                    if (sessionId is null)
                    {
                        await stderr.WriteLineAsync($"Invalid session ID format: '{sessionIdStr}'.");
                        return 1;
                    }
                }

                var state = await sessionManager.LoadSessionStateAsync(sessionId, cancellationToken);
                if (state is null)
                {
                    await stderr.WriteLineAsync($"Session not found: {sessionId}");
                    return 1;
                }

                var metrics = await sessionManager.LoadSessionMetricsAsync(sessionId, cancellationToken);
                var output = FormatSession(state, metrics, format);
                await stdout.WriteLineAsync(output);
                return 0;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });
        return show;
    }

    private static Command CreateResumeCommand(IServiceProvider services, TextWriter stdout, TextWriter stderr)
    {
        var resume = new Command("resume", "Resume a session");
        var sessionIdArg = new Argument<string?>("session-id") { Description = "Session ID (defaults to latest)", Arity = ArgumentArity.ZeroOrOne };
        resume.Add(sessionIdArg);

        resume.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var sessionManager = services.GetRequiredService<ISessionManager>();
            try
            {
                var sessionIdStr = parseResult.GetValue(sessionIdArg);

                SessionId? sessionId;
                if (string.IsNullOrWhiteSpace(sessionIdStr))
                {
                    sessionId = await sessionManager.GetLatestSessionIdAsync(cancellationToken);
                    if (sessionId is null)
                    {
                        await stderr.WriteLineAsync("No active session found to resume.");
                        return 1;
                    }
                }
                else
                {
                    sessionId = SessionId.TryParse(sessionIdStr);
                    if (sessionId is null)
                    {
                        await stderr.WriteLineAsync($"Invalid session ID format: '{sessionIdStr}'.");
                        return 1;
                    }
                }

                var state = await sessionManager.LoadSessionStateAsync(sessionId, cancellationToken);
                if (state is null)
                {
                    await stderr.WriteLineAsync($"Session not found: {sessionId}");
                    return 1;
                }

                if (state.IsComplete)
                {
                    await stderr.WriteLineAsync($"Session {sessionId} is already complete.");
                    return 1;
                }

                await sessionManager.SetLatestAsync(sessionId, cancellationToken);
                await stdout.WriteLineAsync($"Resumed session {sessionId} (phase: {state.Phase}, step: {state.Step}).");
                return 0;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });
        return resume;
    }

    private static Command CreateDeleteCommand(IServiceProvider services, TextWriter stdout, TextWriter stderr)
    {
        var delete = new Command("delete", "Delete a session");
        var sessionIdArg = new Argument<string>("session-id") { Description = "Session ID to delete" };
        delete.Add(sessionIdArg);

        delete.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            var sessionManager = services.GetRequiredService<ISessionManager>();
            try
            {
                var sessionIdStr = parseResult.GetValue(sessionIdArg);
                var sessionId = SessionId.TryParse(sessionIdStr);
                if (sessionId is null)
                {
                    await stderr.WriteLineAsync($"Invalid session ID format: '{sessionIdStr}'.");
                    return 1;
                }

                await sessionManager.DeleteSessionAsync(sessionId, cancellationToken);
                await stdout.WriteLineAsync($"Deleted session {sessionId}.");
                return 0;
            }
            catch (StorageException ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });
        return delete;
    }

    private static Command CreatePruneCommand(IServiceProvider services, TextWriter stdout, TextWriter stderr)
    {
        var prune = new Command("prune", "Remove completed sessions beyond retention limit");
        prune.SetAction(async (ParseResult _, CancellationToken cancellationToken) =>
        {
            var sessionManager = services.GetRequiredService<ISessionManager>();
            var options = services.GetRequiredService<Configuration.LopenOptions>();
            try
            {
                var retention = options.Session.SessionRetention;
                var pruned = await sessionManager.PruneSessionsAsync(retention, cancellationToken);
                await stdout.WriteLineAsync($"Pruned {pruned} session(s) (retention: {retention}).");
                return 0;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });
        return prune;
    }

    public static string FormatSession(SessionState state, SessionMetrics? metrics, string format)
    {
        return format.ToLowerInvariant() switch
        {
            "json" => FormatJson(state, metrics),
            "yaml" => FormatYaml(state, metrics),
            _ => FormatMarkdown(state, metrics),
        };
    }

    private static string FormatMarkdown(SessionState state, SessionMetrics? metrics)
    {
        var lines = new List<string>
        {
            $"# Session: {state.SessionId}",
            "",
            $"- **Phase**: {state.Phase}",
            $"- **Step**: {state.Step}",
            $"- **Module**: {state.Module}",
        };

        if (state.Component is not null)
            lines.Add($"- **Component**: {state.Component}");

        lines.Add($"- **Status**: {(state.IsComplete ? "Complete" : "Active")}");
        lines.Add($"- **Created**: {state.CreatedAt:u}");
        lines.Add($"- **Updated**: {state.UpdatedAt:u}");

        if (state.LastTaskCompletionCommitSha is not null)
            lines.Add($"- **Last Commit**: {state.LastTaskCompletionCommitSha}");

        if (metrics is not null)
        {
            lines.Add("");
            lines.Add("## Metrics");
            lines.Add($"- **Iterations**: {metrics.IterationCount}");
            lines.Add($"- **Input Tokens**: {metrics.CumulativeInputTokens:N0}");
            lines.Add($"- **Output Tokens**: {metrics.CumulativeOutputTokens:N0}");
            lines.Add($"- **Premium Requests**: {metrics.PremiumRequestCount}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatJson(SessionState state, SessionMetrics? metrics)
    {
        var obj = new Dictionary<string, object?>
        {
            ["session_id"] = state.SessionId,
            ["phase"] = state.Phase,
            ["step"] = state.Step,
            ["module"] = state.Module,
            ["component"] = state.Component,
            ["is_complete"] = state.IsComplete,
            ["created_at"] = state.CreatedAt,
            ["updated_at"] = state.UpdatedAt,
            ["last_commit"] = state.LastTaskCompletionCommitSha,
        };

        if (metrics is not null)
        {
            obj["metrics"] = new Dictionary<string, object>
            {
                ["iteration_count"] = metrics.IterationCount,
                ["input_tokens"] = metrics.CumulativeInputTokens,
                ["output_tokens"] = metrics.CumulativeOutputTokens,
                ["premium_requests"] = metrics.PremiumRequestCount,
            };
        }

        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    private static string FormatYaml(SessionState state, SessionMetrics? metrics)
    {
        var lines = new List<string>
        {
            $"session_id: {state.SessionId}",
            $"phase: {state.Phase}",
            $"step: {state.Step}",
            $"module: {state.Module}",
        };

        if (state.Component is not null)
            lines.Add($"component: {state.Component}");

        lines.Add($"is_complete: {state.IsComplete.ToString().ToLowerInvariant()}");
        lines.Add($"created_at: {state.CreatedAt:u}");
        lines.Add($"updated_at: {state.UpdatedAt:u}");

        if (state.LastTaskCompletionCommitSha is not null)
            lines.Add($"last_commit: {state.LastTaskCompletionCommitSha}");

        if (metrics is not null)
        {
            lines.Add("metrics:");
            lines.Add($"  iteration_count: {metrics.IterationCount}");
            lines.Add($"  input_tokens: {metrics.CumulativeInputTokens}");
            lines.Add($"  output_tokens: {metrics.CumulativeOutputTokens}");
            lines.Add($"  premium_requests: {metrics.PremiumRequestCount}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
