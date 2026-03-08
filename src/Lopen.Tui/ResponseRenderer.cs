using System.Text;
using Spectre.Console;

namespace Lopen.Tui;

/// <summary>
/// Renders LLM response content with markdown-aware formatting.
/// Handles code blocks, bullet lists, bold text, and regular text.
/// All output uses Markup.Escape() to prevent injection.
/// </summary>
public sealed class ResponseRenderer(IAnsiConsole console)
{
    private readonly IAnsiConsole _console = console ?? throw new ArgumentNullException(nameof(console));

    /// <summary>
    /// Renders a complete response message with markdown formatting.
    /// </summary>
    public void RenderContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return;

        string[] lines = content.Split('\n');
        bool inCodeBlock = false;
        List<string> codeBlockLines = new List<string>();
        string? codeBlockLanguage = null;

        foreach (string line in lines)
        {
            if (line.TrimStart().StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    // End code block
                    RenderCodeBlock(codeBlockLines, codeBlockLanguage);
                    codeBlockLines.Clear();
                    codeBlockLanguage = null;
                    inCodeBlock = false;
                }
                else
                {
                    // Start code block
                    inCodeBlock = true;
                    string lang = line.TrimStart()[3..].Trim();
                    codeBlockLanguage = string.IsNullOrEmpty(lang) ? null : lang;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBlockLines.Add(line);
                continue;
            }

            // Bullet list items
            if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
            {
                string bulletText = line.TrimStart()[2..];
                _console.MarkupLine($"  {LopenTheme.Styled(LopenTheme.Bullet, LopenTheme.Accent)} {RenderInlineMarkdown(bulletText)}");
                continue;
            }

            // Regular text with inline formatting
            _console.MarkupLine(RenderInlineMarkdown(line));
        }

        // Handle unclosed code block
        if (inCodeBlock && codeBlockLines.Count > 0)
        {
            RenderCodeBlock(codeBlockLines, codeBlockLanguage);
        }
    }

    private void RenderCodeBlock(List<string> lines, string? language)
    {
        string code = string.Join("\n", lines);
        string header = language is not null
            ? LopenTheme.Bold(language, LopenTheme.Muted)
            : LopenTheme.Styled("Code", LopenTheme.Muted);

        Panel panel = new Panel(Markup.Escape(code))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(LopenTheme.Muted),
            Header = new PanelHeader(header),
            Padding = new Padding(1, 0),
        };

        _console.Write(panel);
    }

    /// <summary>
    /// Renders inline markdown formatting (bold) with proper escaping.
    /// </summary>
    internal static string RenderInlineMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        StringBuilder result = new System.Text.StringBuilder();
        int i = 0;

        while (i < text.Length)
        {
            // Check for **bold**
            if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
            {
                int closeIndex = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (closeIndex >= 0)
                {
                    string boldText = text[(i + 2)..closeIndex];
                    result.Append($"[bold]{Markup.Escape(boldText)}[/]");
                    i = closeIndex + 2;
                    continue;
                }
            }

            // Regular character - escape it
            result.Append(Markup.Escape(text[i].ToString()));
            i++;
        }

        return result.ToString();
    }
}
