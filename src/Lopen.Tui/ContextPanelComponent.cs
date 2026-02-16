namespace Lopen.Tui;

/// <summary>
/// Renders the context panel (right pane) with current task, component/module hierarchy,
/// and active resources.
/// </summary>
public sealed class ContextPanelComponent : ITuiComponent
{
    public string Name => "ContextPanel";
    public string Description => "Context panel with task tree, completion states, and active resources";

    /// <summary>
    /// Renders the context panel as an array of plain-text lines sized to the given region width.
    /// </summary>
    public string[] Render(ContextPanelData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();

        if (data.CurrentTask is not null)
            RenderTaskSection(data.CurrentTask, lines);

        if (data.Component is not null)
        {
            if (lines.Count > 0)
                lines.Add(string.Empty);
            RenderComponentSection(data.Component, lines);
        }

        if (data.Module is not null)
        {
            if (lines.Count > 0)
                lines.Add(string.Empty);
            RenderModuleSection(data.Module, lines);
        }

        if (data.Resources.Count > 0)
        {
            if (lines.Count > 0)
                lines.Add(string.Empty);
            RenderResourcesSection(data.Resources, lines);
        }

        // Pad remaining rows
        while (lines.Count < region.Height)
            lines.Add(string.Empty);

        // Trim and pad each line to region width
        return lines
            .Take(region.Height)
            .Select(l => PadToWidth(l, region.Width))
            .ToArray();
    }

    private static void RenderTaskSection(TaskSectionData task, List<string> lines)
    {
        lines.Add($"▶ Current Task: {task.Name}");
        lines.Add($"  Progress: {task.ProgressPercent}% ({task.CompletedSubtasks}/{task.TotalSubtasks} subtasks done)");
        lines.Add($"  {RenderProgressBar(task.ProgressPercent, 20)}");

        for (int i = 0; i < task.Subtasks.Count; i++)
        {
            var sub = task.Subtasks[i];
            var connector = i < task.Subtasks.Count - 1 ? "├─" : "└─";
            lines.Add($"  {connector}{StateIcon(sub.State)} {sub.Name}");
        }
    }

    /// <summary>
    /// Renders a text-based progress bar of the given width.
    /// </summary>
    internal static string RenderProgressBar(int percent, int barWidth = 20)
    {
        percent = Math.Clamp(percent, 0, 100);
        var filled = (int)(barWidth * percent / 100.0);
        var empty = barWidth - filled;
        return $"[{new string('█', filled)}{new string('░', empty)}]";
    }

    private static void RenderComponentSection(ComponentSectionData component, List<string> lines)
    {
        lines.Add($"📊 Component: {component.Name}");
        lines.Add($"   Tasks: {component.CompletedTasks}/{component.TotalTasks} complete");

        for (int i = 0; i < component.Tasks.Count; i++)
        {
            var task = component.Tasks[i];
            var connector = i < component.Tasks.Count - 1 ? "├─" : "└─";
            lines.Add($"   {connector}{StateIcon(task.State)} {task.Name}");
        }
    }

    private static void RenderModuleSection(ModuleSectionData module, List<string> lines)
    {
        lines.Add($"📦 Module: {module.Name}");
        lines.Add($"   Components: {module.InProgressComponents}/{module.TotalComponents} in progress");

        for (int i = 0; i < module.Components.Count; i++)
        {
            var comp = module.Components[i];
            var connector = i < module.Components.Count - 1 ? "├─" : "└─";
            lines.Add($"   {connector}{StateIcon(comp.State)} {comp.Name}");
        }
    }

    private static void RenderResourcesSection(IReadOnlyList<ResourceItem> resources, List<string> lines)
    {
        lines.Add("📚 Active Resources:");
        for (int i = 0; i < resources.Count && i < 9; i++)
        {
            lines.Add($"[{i + 1}] {resources[i].Label}");
        }
        lines.Add("Press 1-9 to view • Auto-tracked & managed");
    }

    /// <summary>
    /// Returns the Unicode icon for a task state.
    /// </summary>
    internal static string StateIcon(TaskState state) => state switch
    {
        TaskState.Pending => "○",
        TaskState.InProgress => "▶",
        TaskState.Complete => "✓",
        TaskState.Failed => "✗",
        _ => "?",
    };

    private static string PadToWidth(string text, int width)
    {
        return text.Length >= width
            ? text[..width]
            : text.PadRight(width);
    }
}
