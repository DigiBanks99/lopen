namespace Lopen.Llm;

/// <summary>
/// Maps startup exceptions from the Copilot SDK client to <see cref="CopilotFailureCategory"/>
/// and actionable user hints. This is the single authoritative policy for categorising
/// client-initialisation failures so TUI rendering and logging both receive stable diagnostics.
/// </summary>
internal static class CopilotStartupFailureMapper
{
    private static readonly string[] AuthKeywords =
        ["401", "403", "unauthorized", "forbidden", "authentication", "credentials", "token", "unauthenticated"];

    private static readonly string[] NetworkKeywords =
        ["network", "connection", "timeout", "unreachable", "host", "dns", "socket", "connect", "refused", "reset"];

    private static readonly string[] ServiceKeywords =
        ["service", "unavailable", "503", "502", "500", "server error", "bad gateway", "service unavailable"];

    private static readonly IReadOnlyDictionary<CopilotFailureCategory, string> UserHints =
        new Dictionary<CopilotFailureCategory, string>
        {
            [CopilotFailureCategory.Auth] =
                "Run 'lopen auth login' or set GH_TOKEN to re-authenticate.",
            [CopilotFailureCategory.Network] =
                "Check your network connection and verify the Copilot service is reachable.",
            [CopilotFailureCategory.Service] =
                "The Copilot service may be temporarily unavailable. Try again in a few moments.",
            [CopilotFailureCategory.Unknown] =
                "Run 'lopen auth status' to check your configuration.",
        };

    /// <summary>
    /// Wraps a startup exception in an <see cref="LlmException"/> enriched with
    /// a deterministic <see cref="CopilotFailureCategory"/> and a user-facing hint.
    /// </summary>
    public static LlmException CreateDiagnosticException(string outerMessage, Exception inner)
    {
        CopilotFailureCategory category = Classify(inner);
        return new LlmException(outerMessage, model: null, inner)
        {
            DiagnosticCategory = category,
            UserHint = UserHints[category],
        };
    }

    /// <summary>
    /// Classifies an exception by inspecting its message chain for well-known keywords.
    /// </summary>
    internal static CopilotFailureCategory Classify(Exception ex)
    {
        string combined = BuildMessageChain(ex);

        if (ContainsAny(combined, AuthKeywords))
            return CopilotFailureCategory.Auth;

        if (ContainsAny(combined, NetworkKeywords))
            return CopilotFailureCategory.Network;

        if (ContainsAny(combined, ServiceKeywords))
            return CopilotFailureCategory.Service;

        return CopilotFailureCategory.Unknown;
    }

    private static string BuildMessageChain(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        Exception? current = ex;
        while (current is not null)
        {
            sb.Append(current.Message);
            sb.Append(' ');
            current = current.InnerException;
        }
        return sb.ToString();
    }

    private static bool ContainsAny(string text, string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
