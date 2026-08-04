using Lopen.Llm;
using Spectre.Console;

namespace Lopen.Tui.Gallery;

/// <summary>
/// Gallery component showing workflow overview in all states:
/// no workflow, active (building), paused, and complete.
/// </summary>
public sealed class WorkflowOverviewGalleryComponent : IGalleryComponent
{
    public string DisplayName => "Workflow Overview (4 states)";

    public void Render(IAnsiConsole console)
    {
        // State 1: No workflow (initial state)
        console.MarkupLine(LopenTheme.Bold("1. No Workflow", LopenTheme.Accent));
        string noWorkflow = $"{LopenTheme.Styled("tokens: 0", LopenTheme.Muted)}    {LopenTheme.Styled("model: gpt-4.1", LopenTheme.Accent)}";
        console.Write(new Panel(new Markup(noWorkflow))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader(LopenTheme.Bold("lopen", LopenTheme.Primary)),
            Expand = true,
        });

        console.WriteLine();

        // State 2: Active workflow (Building phase)
        console.MarkupLine(LopenTheme.Bold("2. Active Workflow (Build phase)", LopenTheme.Accent));
        string activePhases = WorkflowOverviewBlock.BuildPhaseDisplay(
            WorkflowPhase.Building, false);
        string activeTokens = $"{LopenTheme.Styled("tokens: 12.5k/100k", LopenTheme.Muted)}    {LopenTheme.Styled("model: gpt-4.1", LopenTheme.Accent)}";
        console.Write(new Panel(new Markup($"{activePhases}\n{LopenTheme.Styled("tui: task 3/5 \u2014 register-gallery-components", LopenTheme.Muted)}\n{activeTokens}"))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader(LopenTheme.Bold("lopen", LopenTheme.Primary)),
            Expand = true,
        });

        console.WriteLine();

        // State 3: Paused
        console.MarkupLine(LopenTheme.Bold("3. Paused", LopenTheme.Accent));
        string pausedIndicator = LopenTheme.Styled($"{LopenTheme.PauseIndicator} PAUSED", LopenTheme.Warning);
        string pausedPhases = WorkflowOverviewBlock.BuildPhaseDisplay(
            WorkflowPhase.Building, false);
        string pausedTokens = $"{LopenTheme.Styled("tokens: 45.2k/100k", LopenTheme.Muted)}    {LopenTheme.Styled("model: gpt-4.1", LopenTheme.Accent)}";
        console.Write(new Panel(new Markup($"{pausedIndicator}\n{pausedPhases}\n{pausedTokens}"))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader(LopenTheme.Bold("lopen", LopenTheme.Primary)),
            Expand = true,
        });

        console.WriteLine();

        // State 4: Complete
        console.MarkupLine(LopenTheme.Bold("4. Complete", LopenTheme.Accent));
        string completePhases = WorkflowOverviewBlock.BuildPhaseDisplay(
            WorkflowPhase.Building, true);
        string completeTokens = $"{LopenTheme.Styled("tokens: 87.3k/100k", LopenTheme.Muted)}    {LopenTheme.Styled("model: gpt-4.1", LopenTheme.Accent)}";
        console.Write(new Panel(new Markup($"{completePhases}\n{completeTokens}"))
        {
            Border = BoxBorder.Rounded,
            Header = new PanelHeader(LopenTheme.Bold("lopen", LopenTheme.Primary)),
            Expand = true,
        });
    }
}
