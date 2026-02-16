using Lopen.Storage;

namespace Lopen.Tui;

/// <summary>
/// Detects active sessions via <see cref="ISessionManager"/> and maps
/// <see cref="SessionState"/> to <see cref="SessionResumeData"/> for display.
/// </summary>
public sealed class SessionDetector : ISessionDetector
{
    private readonly ISessionManager _sessionManager;

    public SessionDetector(ISessionManager sessionManager)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
    }

    public async Task<SessionResumeData?> DetectActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        var sessionId = await _sessionManager.GetLatestSessionIdAsync(cancellationToken).ConfigureAwait(false);
        if (sessionId is null)
            return null;

        var state = await _sessionManager.LoadSessionStateAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (state is null || state.IsComplete)
            return null;

        return MapToResumeData(state);
    }

    internal static SessionResumeData MapToResumeData(SessionState state)
    {
        var lastActivity = FormatRelativeTime(state.UpdatedAt);
        var stepNumber = ParseStepNumber(state.Step);
        const int totalSteps = 7; // WorkflowStep has 7 values
        var progressPercent = totalSteps > 0 ? (int)(stepNumber * 100.0 / totalSteps) : 0;

        return new SessionResumeData
        {
            ModuleName = state.Module,
            PhaseName = state.Phase,
            StepProgress = $"{stepNumber}/{totalSteps}",
            ProgressPercent = Math.Clamp(progressPercent, 0, 100),
            TaskProgress = state.Component is not null ? $"Component: {state.Component}" : "No component selected",
            LastActivity = lastActivity
        };
    }

    internal static string FormatRelativeTime(DateTimeOffset timestamp)
    {
        var elapsed = DateTimeOffset.UtcNow - timestamp;

        return elapsed.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)elapsed.TotalMinutes} minutes ago",
            < 1440 => $"{(int)elapsed.TotalHours} hours ago",
            _ => $"{(int)elapsed.TotalDays} days ago"
        };
    }

    internal static int ParseStepNumber(string stepName)
    {
        // Map known step names to numbers (0-based index + 1 for display)
        return stepName switch
        {
            "DraftSpecification" => 1,
            "DetermineDependencies" => 2,
            "IdentifyComponents" => 3,
            "SelectNextComponent" => 4,
            "BreakIntoTasks" => 5,
            "IterateThroughTasks" => 6,
            "Repeat" => 7,
            _ => 0
        };
    }
}
