using System.Text.RegularExpressions;

namespace Lopen.Core.Documents;

/// <summary>
/// Provides programmatic updates to markdown documents without LLM invocation.
/// Handles task checkbox toggling and status tracking in plan documents.
/// </summary>
public static partial class MarkdownUpdater
{
    /// <summary>
    /// Toggles a markdown checkbox for the task matching the given text.
    /// Converts <c>- [ ] task text</c> to <c>- [x] task text</c> or vice versa.
    /// </summary>
    /// <param name="content">The markdown document content.</param>
    /// <param name="taskText">The task text to match (case-insensitive, trimmed).</param>
    /// <param name="completed">True to mark complete, false to mark incomplete.</param>
    /// <returns>The updated markdown content, or the original if no match found.</returns>
    public static string ToggleCheckbox(string content, string taskText, bool completed)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(taskText);

        var trimmed = taskText.Trim();
        var escapedText = Regex.Escape(trimmed);

        // Match "- [ ] text" or "- [x] text" (with optional leading whitespace)
        var pattern = $@"^(\s*- \[)[ xX](\] {escapedText})";
        var replacement = completed ? "$1x$2" : "$1 $2";

        return Regex.Replace(content, pattern, replacement, RegexOptions.Multiline | RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Counts the total and completed checkboxes in a markdown document.
    /// </summary>
    /// <param name="content">The markdown document content.</param>
    /// <returns>A tuple of (total, completed) checkbox counts.</returns>
    public static (int Total, int Completed) CountCheckboxes(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var total = CheckboxPattern().Matches(content).Count;
        var completed = CompletedCheckboxPattern().Matches(content).Count;
        return (total, completed);
    }

    /// <summary>
    /// Replaces a status label in a markdown document. Useful for component/module status tracking.
    /// Pattern: <c>**Status**: old_value</c> → <c>**Status**: new_value</c>
    /// </summary>
    public static string UpdateStatus(string content, string label, string newValue)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(newValue);

        var escapedLabel = Regex.Escape(label);
        var pattern = $@"(\*\*{escapedLabel}\*\*:\s*)(\S[^\r\n]*)";
        return Regex.Replace(content, pattern, $"$1{newValue}", RegexOptions.IgnoreCase);
    }

    [GeneratedRegex(@"^\s*- \[[ xX]\]", RegexOptions.Multiline)]
    private static partial Regex CheckboxPattern();

    [GeneratedRegex(@"^\s*- \[[xX]\]", RegexOptions.Multiline)]
    private static partial Regex CompletedCheckboxPattern();
}
