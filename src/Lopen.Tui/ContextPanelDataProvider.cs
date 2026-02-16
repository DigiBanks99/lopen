using Lopen.Core.Workflow;
using Lopen.Storage;
using Microsoft.Extensions.Logging;

namespace Lopen.Tui;

/// <summary>
/// Aggregates live plan data and workflow state into <see cref="ContextPanelData"/> snapshots.
/// Plan tasks are cached and refreshed periodically. Workflow state is read fresh each call.
/// </summary>
internal sealed class ContextPanelDataProvider : IContextPanelDataProvider
{
    private readonly IPlanManager _planManager;
    private readonly IWorkflowEngine _workflowEngine;
    private readonly ILogger<ContextPanelDataProvider> _logger;

    private volatile string? _activeModule;
    private volatile IReadOnlyList<PlanTask>? _cachedTasks;

    public ContextPanelDataProvider(
        IPlanManager planManager,
        IWorkflowEngine workflowEngine,
        ILogger<ContextPanelDataProvider> logger)
    {
        _planManager = planManager ?? throw new ArgumentNullException(nameof(planManager));
        _workflowEngine = workflowEngine ?? throw new ArgumentNullException(nameof(workflowEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void SetActiveModule(string moduleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        _activeModule = moduleName;
        _cachedTasks = null; // invalidate cache on module change
    }

    public ContextPanelData GetCurrentData()
    {
        var tasks = _cachedTasks;
        var module = _activeModule;

        if (tasks is null || tasks.Count == 0 || module is null)
            return new ContextPanelData();

        return BuildContextData(module, tasks);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var module = _activeModule;
        if (module is null)
            return;

        try
        {
            _cachedTasks = await _planManager.ReadTasksAsync(module, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Failed to refresh plan tasks for module {Module}", module);
            // Keep stale cache on failure
        }
    }

    internal static ContextPanelData BuildContextData(string moduleName, IReadOnlyList<PlanTask> tasks)
    {
        // Level 0 = components, Level 1 = tasks, Level 2+ = subtasks
        var componentItems = new List<SubtaskItem>();
        ComponentSectionData? componentSection = null;
        TaskSectionData? taskSection = null;

        // Track the first in-progress component and task
        string? currentComponentName = null;
        string? currentTaskName = null;
        var componentTasks = new List<SubtaskItem>();
        var taskSubtasks = new List<SubtaskItem>();

        int totalComponents = 0;
        int completedComponents = 0;

        for (int i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];

            if (task.Level == 0)
            {
                // Finalize previous component if we're tracking one
                FinalizeComponent(ref componentSection, currentComponentName, componentTasks, ref currentTaskName, taskSubtasks, ref taskSection);

                totalComponents++;
                var state = MapTaskState(task, tasks, i);
                componentItems.Add(new SubtaskItem(task.Text, state));

                if (state == TaskState.Complete)
                    completedComponents++;

                // If this is the first in-progress component, start tracking its tasks
                if (currentComponentName is null && !task.IsCompleted)
                {
                    currentComponentName = task.Text;
                    componentTasks = [];
                    currentTaskName = null;
                    taskSubtasks = [];
                }
                else if (currentComponentName is not null && currentComponentName != task.Text)
                {
                    // We've moved past the current component
                    currentComponentName = null;
                }
            }
            else if (task.Level == 1 && currentComponentName is not null)
            {
                // Finalize previous task if tracking one
                FinalizeTask(ref taskSection, currentTaskName, taskSubtasks);

                var state = task.IsCompleted ? TaskState.Complete : TaskState.Pending;
                componentTasks.Add(new SubtaskItem(task.Text, state));

                // If this is the first in-progress task in the current component, track subtasks
                if (currentTaskName is null && !task.IsCompleted)
                {
                    currentTaskName = task.Text;
                    taskSubtasks = [];
                }
                else if (currentTaskName is not null && currentTaskName != task.Text)
                {
                    currentTaskName = null;
                }
            }
            else if (task.Level >= 2 && currentTaskName is not null)
            {
                var state = task.IsCompleted ? TaskState.Complete : TaskState.Pending;
                taskSubtasks.Add(new SubtaskItem(task.Text, state));
            }
        }

        // Finalize the last component/task
        FinalizeComponent(ref componentSection, currentComponentName, componentTasks, ref currentTaskName, taskSubtasks, ref taskSection);

        // Mark the first incomplete item at each level as InProgress
        MarkCurrentInProgress(componentItems);
        if (componentSection is not null)
            MarkCurrentInProgress(componentSection.Tasks);
        if (taskSection is not null)
            MarkCurrentInProgress(taskSection.Subtasks);

        var inProgressComponents = componentItems.Count(c => c.State == TaskState.InProgress);

        var moduleSection = new ModuleSectionData
        {
            Name = moduleName,
            InProgressComponents = inProgressComponents,
            TotalComponents = totalComponents,
            Components = componentItems,
        };

        return new ContextPanelData
        {
            CurrentTask = taskSection,
            Component = componentSection,
            Module = moduleSection,
        };
    }

    private static TaskState MapTaskState(PlanTask task, IReadOnlyList<PlanTask> allTasks, int index)
    {
        if (task.IsCompleted)
            return TaskState.Complete;

        // Check if any child tasks exist and if any are completed (indicates in-progress)
        for (int j = index + 1; j < allTasks.Count; j++)
        {
            if (allTasks[j].Level <= task.Level)
                break;
            if (allTasks[j].IsCompleted)
                return TaskState.InProgress;
        }

        return TaskState.Pending;
    }

    private static void FinalizeComponent(
        ref ComponentSectionData? componentSection,
        string? componentName,
        List<SubtaskItem> componentTasks,
        ref string? currentTaskName,
        List<SubtaskItem> taskSubtasks,
        ref TaskSectionData? taskSection)
    {
        FinalizeTask(ref taskSection, currentTaskName, taskSubtasks);

        if (componentName is not null && componentSection is null && componentTasks.Count > 0)
        {
            var completed = componentTasks.Count(t => t.State == TaskState.Complete);
            componentSection = new ComponentSectionData
            {
                Name = componentName,
                CompletedTasks = completed,
                TotalTasks = componentTasks.Count,
                Tasks = componentTasks.ToList(),
            };
        }

        currentTaskName = null;
    }

    private static void FinalizeTask(
        ref TaskSectionData? taskSection,
        string? taskName,
        List<SubtaskItem> subtasks)
    {
        if (taskName is not null && taskSection is null && subtasks.Count > 0)
        {
            var completed = subtasks.Count(s => s.State == TaskState.Complete);
            var total = subtasks.Count;
            var percent = total > 0 ? (int)(completed * 100.0 / total) : 0;
            taskSection = new TaskSectionData
            {
                Name = taskName,
                ProgressPercent = percent,
                CompletedSubtasks = completed,
                TotalSubtasks = total,
                Subtasks = subtasks.ToList(),
            };
        }
    }

    private static void MarkCurrentInProgress(IReadOnlyList<SubtaskItem> items)
    {
        if (items is not List<SubtaskItem> list)
            return;

        // Don't mark anything if there's already an InProgress item
        if (list.Any(i => i.State == TaskState.InProgress))
            return;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].State == TaskState.Pending)
            {
                list[i] = list[i] with { State = TaskState.InProgress };
                break;
            }
        }
    }
}
