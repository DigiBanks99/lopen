using Spectre.Console;

namespace Lopen.Tui.Gallery;

/// <summary>
/// Gallery component demonstrating response rendering: thinking, content, tool calls, stats.
/// </summary>
public sealed class ResponseRenderingGalleryComponent : IGalleryComponent
{
    public string DisplayName => "Response Rendering";

    public void Render(IAnsiConsole console)
    {
        // 1. Thinking indicator
        console.MarkupLine($"{LopenTheme.SectionMarker} {LopenTheme.Styled("Thinking...", LopenTheme.Muted)}");
        console.WriteLine();

        // 2. Response content with markdown
        ResponseRenderer renderer = new(console);
        renderer.RenderContent("I'll help you refactor that code. Here's my approach:\n\n" +
            "- **Extract** the method into a separate class\n" +
            "- **Add** proper error handling\n" +
            "- **Write** unit tests\n\n" +
            "Here's the refactored code:\n\n" +
            "```csharp\npublic sealed class GreetingService\n{\n    public void Greet(string name)\n    {\n        ArgumentNullException.ThrowIfNull(name);\n        Console.WriteLine($\"Hello, {name}!\");\n    }\n}\n```");

        console.WriteLine();

        // 3. Tool calls
        ToolCallRenderer toolRenderer = new(console);
        toolRenderer.RenderSuccess("read_file", TimeSpan.FromMilliseconds(45), "public class Foo { ... }");
        toolRenderer.RenderSuccess("write_file", TimeSpan.FromMilliseconds(120));
        toolRenderer.RenderFailure("run_tests", "2 tests failed", TimeSpan.FromSeconds(3.2));

        console.WriteLine();

        // 4. Stats bar
        StatsBar statsBar = new(console);
        statsBar.Render(1250, TimeSpan.FromSeconds(4.7), "gpt-4.1");
    }
}
