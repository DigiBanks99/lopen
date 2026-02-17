namespace Lopen.Tui;

/// <summary>
/// Renders the guided conversation UI for requirement gathering (TUI-22).
/// Shows Q&amp;A turns, current question, progress, and drafted spec.
/// </summary>
public sealed class GuidedConversationComponent : IPreviewableComponent
{
    public string Name => "GuidedConversation";
    public string Description => "Guided Q&A conversation for requirement gathering";

    public IReadOnlyList<string> GetPreviewStates() => ["empty", "populated", "error", "loading"];

    public string[] RenderPreview(string state, int width, int height)
    {
        var data = state switch
        {
            "empty" => new GuidedConversationData(),
            "error" => new GuidedConversationData
            {
                Phase = ConversationPhase.Interview,
                CurrentQuestion = "Error: Could not parse requirements. Please try again.",
            },
            "loading" => new GuidedConversationData
            {
                Phase = ConversationPhase.Drafting,
                Turns = [
                    new() { Role = ConversationRole.Agent, Content = "What is the main goal?" },
                    new() { Role = ConversationRole.User, Content = "Build an auth module" },
                ],
                QuestionsAnswered = 2,
                EstimatedTotalQuestions = 5,
            },
            _ => new GuidedConversationData // "populated"
            {
                Phase = ConversationPhase.Interview,
                Turns = [
                    new() { Role = ConversationRole.Agent, Content = "Describe your project idea." },
                    new() { Role = ConversationRole.User, Content = "I want to build a REST API for user management." },
                    new() { Role = ConversationRole.Agent, Content = "What authentication method should be used?" },
                    new() { Role = ConversationRole.User, Content = "OAuth 2.0 with JWT tokens." },
                ],
                CurrentQuestion = "Are there any specific rate limiting requirements?",
                QuestionsAnswered = 2,
                EstimatedTotalQuestions = 5,
            },
        };
        return Render(data, new ScreenRect(0, 0, width, height));
    }

    public string[] RenderPreview(int width, int height)
        => RenderPreview("populated", width, height);

    /// <summary>
    /// Renders the guided conversation as an array of lines.
    /// Layout: phase header, conversation turns, current question, progress bar.
    /// </summary>
    public string[] Render(GuidedConversationData data, ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
            return [];

        var lines = new List<string>();
        var width = region.Width;

        // Phase header
        var phaseLabel = data.Phase switch
        {
            ConversationPhase.Ideation => "Ideation — Share your idea",
            ConversationPhase.Interview => "Interview — Gathering requirements",
            ConversationPhase.Drafting => "Drafting — Writing specification...",
            ConversationPhase.Reviewing => "Review — Check the drafted spec",
            ConversationPhase.Complete => "Complete — Specification approved",
            _ => "Conversation",
        };
        lines.Add(PadToWidth($"── {phaseLabel} ──", width));

        // Progress (if in interview or later)
        if (data.EstimatedTotalQuestions > 0)
        {
            var progress = $"[{data.QuestionsAnswered}/{data.EstimatedTotalQuestions}]";
            lines.Add(PadToWidth(progress, width));
        }

        // Conversation turns
        foreach (var turn in data.Turns)
        {
            var prefix = turn.Role == ConversationRole.Agent ? "? " : "> ";
            var turnLines = WrapText($"{prefix}{turn.Content}", width);
            foreach (var tl in turnLines)
                lines.Add(PadToWidth(tl, width));
        }

        // Current question
        if (!string.IsNullOrEmpty(data.CurrentQuestion))
        {
            lines.Add(PadToWidth(string.Empty, width));
            var qLines = WrapText($"? {data.CurrentQuestion}", width);
            foreach (var ql in qLines)
                lines.Add(PadToWidth(ql, width));
        }

        // Drafted spec preview
        if (!string.IsNullOrEmpty(data.DraftedSpec))
        {
            lines.Add(PadToWidth("─── Drafted Specification ───", width));
            var specLines = data.DraftedSpec.Split('\n');
            foreach (var sl in specLines)
                lines.Add(PadToWidth(sl, width));
        }

        // Pad or truncate to exact height
        while (lines.Count < region.Height)
            lines.Add(PadToWidth(string.Empty, width));

        return lines.GetRange(0, region.Height).ToArray();
    }

    internal static List<string> WrapText(string text, int width)
    {
        if (width <= 0) return [string.Empty];
        var result = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            if (line.Length <= width)
                result.Add(line);
            else
                for (int i = 0; i < line.Length; i += width)
                    result.Add(line.Substring(i, Math.Min(width, line.Length - i)));
        }
        return result;
    }

    private static string PadToWidth(string text, int width)
        => text.Length >= width ? text[..width] : text.PadRight(width);
}
