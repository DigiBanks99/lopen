using Lopen.Core.Workflow;
using RadLine;

namespace Lopen.Tui;

/// <summary>
/// RadLine command bound to Ctrl+P: toggles orchestrator pause/resume via <see cref="IPauseController"/>.
/// The user remains in the editor after toggling — no submit or cancel action is taken.
/// </summary>
internal sealed class TogglePauseCommand(IPauseController pauseController) : LineEditorCommand
{
    public override void Execute(LineEditorContext context)
    {
        pauseController.Toggle();
    }
}
