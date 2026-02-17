using System.Text.RegularExpressions;

namespace Lopen.Tui;

/// <summary>
/// Lightweight syntax highlighter for code in diff views (TUI-15).
/// Maps file extensions to keyword sets and applies ANSI coloring.
/// </summary>
public static partial class SyntaxHighlighter
{
    private static readonly Dictionary<string, HashSet<string>> KeywordsByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = ["public", "private", "protected", "internal", "static", "sealed", "abstract",
                   "class", "interface", "record", "struct", "enum", "namespace", "using",
                   "if", "else", "return", "var", "new", "null", "true", "false", "this",
                   "async", "await", "readonly", "const", "override", "virtual", "is", "not"],
        [".ts"] = ["const", "let", "var", "function", "class", "interface", "type", "enum",
                   "import", "export", "from", "return", "if", "else", "null", "undefined",
                   "true", "false", "async", "await", "new", "this", "extends", "implements"],
        [".js"] = ["const", "let", "var", "function", "class", "return", "if", "else",
                   "null", "undefined", "true", "false", "async", "await", "new", "this",
                   "import", "export", "from", "extends"],
        [".py"] = ["def", "class", "return", "if", "elif", "else", "import", "from",
                   "None", "True", "False", "self", "async", "await", "with", "as",
                   "for", "in", "while", "try", "except", "raise", "pass", "lambda"],
    };

    // ANSI colors for syntax elements
    private const string KeywordColor = "\x1b[34m";  // Blue
    private const string StringColor = "\x1b[32m";    // Green
    private const string CommentColor = "\x1b[90m";   // Gray
    private const string Reset = "\x1b[0m";

    /// <summary>
    /// Applies language-aware syntax highlighting to a code line.
    /// Returns the original line if no extension mapping exists.
    /// </summary>
    public static string HighlightLine(string line, string? fileExtension)
    {
        if (string.IsNullOrEmpty(fileExtension) || !KeywordsByExtension.TryGetValue(fileExtension, out var keywords))
            return line;

        // Highlight string literals (simple: single and double quotes)
        var result = StringPattern().Replace(line, match =>
            $"{StringColor}{match.Value}{Reset}");

        // Highlight single-line comments
        result = CommentPattern().Replace(result, match =>
            $"{CommentColor}{match.Value}{Reset}");

        // Highlight keywords (word boundaries)
        result = WordPattern().Replace(result, match =>
            keywords.Contains(match.Value)
                ? $"{KeywordColor}{match.Value}{Reset}"
                : match.Value);

        return result;
    }

    /// <summary>
    /// Determines if syntax highlighting is available for a file extension.
    /// </summary>
    public static bool SupportsExtension(string? fileExtension) =>
        fileExtension is not null && KeywordsByExtension.ContainsKey(fileExtension);

    [GeneratedRegex("""("(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*')""")]
    private static partial Regex StringPattern();

    [GeneratedRegex(@"(//.*|#.*)$")]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"\b[a-zA-Z_]\w*\b")]
    private static partial Regex WordPattern();
}
