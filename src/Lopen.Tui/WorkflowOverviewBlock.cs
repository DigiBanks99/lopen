using Lopen.Configuration;
using Lopen.Core.Workflow;
using Lopen.Llm;
using Lopen.Storage;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// Renders the workflow overview panel before each prompt.
/// Shows phase indicators, task progress, token count, and current model.
/// </summary>
public sealed class WorkflowOverviewBlock(
    IAnsiConsole console,
    IOptions<LopenOptions> options,
    IWorkflowEngine? workflowEngine = null,
    ITokenTracker? tokenTracker = null,
    IPauseController? pauseController = null)
{
    private readonly IAnsiConsole _console = console ?? throw new ArgumentNullException(nameof(console));
    private readonly IOptions<LopenOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IWorkflowEngine? _workflowEngine = workflowEngine;
    private readonly ITokenTracker? _tokenTracker = tokenTracker;
    private readonly IPauseController? _pauseController = pauseController;

    private string? _lastModel;

    /// <summary>
    /// Renders the overview block. Call before each prompt.
    /// </summary>
    public void Render(SessionState? sessionState = null)
    {
        string currentModel = GetCurrentModel();

        // Check for model change notification
        if (_lastModel is not null && _lastModel != currentModel)
        {
            _console.MarkupLine($"{LopenTheme.SectionMarker} {LopenTheme.Styled($"Model switched to {currentModel}", LopenTheme.Accent)}");
        }
        _lastModel = currentModel;

        List<string> lines = new List<string>();

        // Paused indicator
        if (_pauseController?.IsPaused == true)
        {
            lines.Add($"{LopenTheme.Styled($"{LopenTheme.PauseIndicator} PAUSED", LopenTheme.Warning)}");
        }

        // Phase indicators (only if workflow engine is available and has started)
        if (_workflowEngine is not null)
        {
            string phaseDisplay = BuildPhaseDisplay(_workflowEngine.CurrentPhase, _workflowEngine.IsComplete);
            lines.Add(phaseDisplay);
        }

        // Task progress (only if session state has task info)
        if (sessionState is not null)
        {
            string? taskLine = BuildTaskLine(sessionState);
            if (taskLine is not null)
            {
                lines.Add(taskLine);
            }
        }

        // Token count and model (always shown)
        string tokenLine = BuildTokenLine(currentModel);
        lines.Add(tokenLine);

        string content = string.Join("\n", lines);
        Panel panel = new Panel(new Markup(content))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader(LopenTheme.Bold("lopen", LopenTheme.Primary)),
            Expand = true,
        };

        _console.Write(panel);
    }

    private string GetCurrentModel()
    {
        return _options.Value.Models.Building;
    }

    internal static string BuildPhaseDisplay(WorkflowPhase currentPhase, bool isComplete)
    {
        IReadOnlyList<DisplayPhase> phases = MapPhases(currentPhase, isComplete);
        return string.Join("  ", phases.Select(p =>
        {
            (string? indicator, Color color) = p.State switch
            {
                DisplayPhaseState.Complete => (LopenTheme.PhaseComplete, LopenTheme.Success),
                DisplayPhaseState.Active => (LopenTheme.PhaseActive, LopenTheme.Primary),
                DisplayPhaseState.Pending => (LopenTheme.PhasePending, LopenTheme.Muted),
                _ => (LopenTheme.PhasePending, LopenTheme.Muted),
            };
            return $"{LopenTheme.Styled(indicator, color)} {LopenTheme.Styled(p.Name, color)}";
        }));
    }

    internal static IReadOnlyList<DisplayPhase> MapPhases(WorkflowPhase currentPhase, bool isComplete)
    {
        if (isComplete)
        {
            return
            [
                new("Assess", DisplayPhaseState.Complete),
                new("Plan", DisplayPhaseState.Complete),
                new("Build", DisplayPhaseState.Complete),
                new("Verify", DisplayPhaseState.Complete),
            ];
        }

        return currentPhase switch
        {
            WorkflowPhase.RequirementGathering =>
            [
                new("Assess", DisplayPhaseState.Active),
                new("Plan", DisplayPhaseState.Pending),
                new("Build", DisplayPhaseState.Pending),
                new("Verify", DisplayPhaseState.Pending),
            ],
            WorkflowPhase.Planning =>
            [
                new("Assess", DisplayPhaseState.Complete),
                new("Plan", DisplayPhaseState.Active),
                new("Build", DisplayPhaseState.Pending),
                new("Verify", DisplayPhaseState.Pending),
            ],
            WorkflowPhase.Building =>
            [
                new("Assess", DisplayPhaseState.Complete),
                new("Plan", DisplayPhaseState.Complete),
                new("Build", DisplayPhaseState.Active),
                new("Verify", DisplayPhaseState.Pending),
            ],
            _ => // Research or unknown
            [
                new("Assess", DisplayPhaseState.Pending),
                new("Plan", DisplayPhaseState.Pending),
                new("Build", DisplayPhaseState.Pending),
                new("Verify", DisplayPhaseState.Pending),
            ],
        };
    }

    private static string? BuildTaskLine(SessionState state)
    {
        string module = state.Module;
        if (string.IsNullOrEmpty(module))
            return null;

        List<string> parts = new List<string> { Markup.Escape(module) };

        // Count tasks and find active one
        if (state.TaskHierarchy is { Count: > 0 })
        {
            (int completed, int total, string? activeName) = CountTasks(state.TaskHierarchy);
            if (total > 0)
            {
                string taskInfo = $"task {completed}/{total}";
                if (activeName is not null)
                {
                    taskInfo += $" \u2014 {Markup.Escape(activeName)}";
                }
                parts.Add(taskInfo);
            }
        }

        return LopenTheme.Styled(string.Join(": ", parts), LopenTheme.Muted);
    }

    internal static (int completed, int total, string? activeName) CountTasks(IReadOnlyList<TaskHierarchyNode> nodes)
    {
        int completed = 0;
        int total = 0;
        string? activeName = null;

        foreach (TaskHierarchyNode node in nodes)
        {
            if (node.NodeType is "task" or "subtask")
            {
                total++;
                if (node.State == "Complete")
                    completed++;
                else if (node.State == "InProgress" && activeName is null)
                    activeName = node.Name;
            }

            if (node.Children.Count > 0)
            {
                (int childCompleted, int childTotal, string? childActive) = CountTasks(node.Children);
                completed += childCompleted;
                total += childTotal;
                activeName ??= childActive;
            }
        }

        return (completed, total, activeName);
    }

    private string BuildTokenLine(string model)
    {
        SessionTokenMetrics? metrics = _tokenTracker?.GetSessionMetrics();
        int totalTokens = metrics is not null
            ? metrics.CumulativeInputTokens + metrics.CumulativeOutputTokens
            : 0;

        string tokenStr = FormatTokenCount(totalTokens);
        int budget = _options.Value.Budget.TokenBudgetPerModule;
        string tokenDisplay = budget > 0
            ? $"tokens: {tokenStr}/{FormatTokenCount(budget)}"
            : $"tokens: {tokenStr}";

        return $"{LopenTheme.Styled(tokenDisplay, LopenTheme.Muted)}    {LopenTheme.Styled($"model: {model}", LopenTheme.Accent)}";
    }

    internal static string FormatTokenCount(int count)
    {
        return count switch
        {
            >= 1_000_000 => $"{count / 1_000_000.0:0.#}M",
            >= 1_000 => $"{count / 1_000.0:0.#}k",
            _ => count.ToString(),
        };
    }
}

/// <summary>State of a display phase in the overview block.</summary>
public enum DisplayPhaseState
{
    Pending,
    Active,
    Complete,
}

/// <summary>A display phase with name and state.</summary>
/// <param name="Name">Display name (Assess, Plan, Build, Verify).</param>
/// <param name="State">Current state of this phase.</param>
public sealed record DisplayPhase(string Name, DisplayPhaseState State);
