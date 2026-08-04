using RadLine;

namespace Lopen.Tui;

/// <summary>
/// RadLine command bound to Ctrl+D: exits Lopen when the input buffer is empty.
/// When the buffer contains text, the key press is ignored.
/// </summary>
internal sealed class ExitOnEmptyCommand(LopenLineEditor.ExitRequestedFlag exitFlag) : LineEditorCommand
{
    public override void Execute(LineEditorContext context)
    {
        if (context.Buffer.Length == 0)
        {
            exitFlag.IsSet = true;
            context.Submit(SubmitAction.Cancel);
        }
    }
}
