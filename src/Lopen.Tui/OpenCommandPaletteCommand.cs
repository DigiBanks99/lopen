using RadLine;

namespace Lopen.Tui;

/// <summary>
/// RadLine command bound to Ctrl+O: signals TuiRunner to open the command palette.
/// Cancels the current line editor session so control returns to the REPL loop.
/// </summary>
internal sealed class OpenCommandPaletteCommand(LopenLineEditor.CommandPaletteRequestedFlag flag) : LineEditorCommand
{
    public override void Execute(LineEditorContext context)
    {
        flag.IsSet = true;
        context.Submit(SubmitAction.Cancel);
    }
}
