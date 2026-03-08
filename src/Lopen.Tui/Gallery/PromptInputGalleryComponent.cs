using Spectre.Console;

namespace Lopen.Tui.Gallery;

/// <summary>
/// Gallery component demonstrating prompt input capabilities.
/// </summary>
public sealed class PromptInputGalleryComponent : IGalleryComponent
{
    public string DisplayName => "Prompt Input";

    public void Render(IAnsiConsole console)
    {
        console.MarkupLine(LopenTheme.Bold("Prompt Input Capabilities", LopenTheme.Accent));
        console.WriteLine();

        // Show a mock prompt with various states
        console.MarkupLine($"{LopenTheme.Styled(LopenTheme.PromptChar, LopenTheme.Primary)} Hello, I need help with my project");
        console.WriteLine();

        console.MarkupLine(LopenTheme.Styled("Features:", LopenTheme.Accent));
        console.MarkupLine($"  {LopenTheme.Styled(LopenTheme.Bullet, LopenTheme.Accent)} Character-by-character input with RadLine");
        console.MarkupLine($"  {LopenTheme.Styled(LopenTheme.Bullet, LopenTheme.Accent)} Cursor movement: \u2190/\u2192, Home/End, Ctrl+\u2190/\u2192");
        console.MarkupLine($"  {LopenTheme.Styled(LopenTheme.Bullet, LopenTheme.Accent)} Command history: \u2191/\u2193 arrows");
        console.MarkupLine($"  {LopenTheme.Styled(LopenTheme.Bullet, LopenTheme.Accent)} Multi-line input: Shift+Enter");
        console.MarkupLine($"  {LopenTheme.Styled(LopenTheme.Bullet, LopenTheme.Accent)} Tab completion for /commands");
        console.WriteLine();

        // Multi-line example
        console.MarkupLine(LopenTheme.Styled("Multi-line input example:", LopenTheme.Muted));
        console.MarkupLine($"{LopenTheme.Styled(LopenTheme.PromptChar, LopenTheme.Primary)} Please refactor this code:");
        console.MarkupLine($"  {LopenTheme.Styled("public void Foo() {{", LopenTheme.Text)}");
        console.MarkupLine($"  {LopenTheme.Styled("    Console.WriteLine(\"bar\");", LopenTheme.Text)}");
        console.MarkupLine($"  {LopenTheme.Styled("}}", LopenTheme.Text)}");
    }
}
