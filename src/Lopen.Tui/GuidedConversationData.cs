namespace Lopen.Tui;

/// <summary>
/// Immutable data model for the guided conversation during requirement gathering.
/// Represents an iterative Q&amp;A session where Lopen interviews the user to shape a specification.
/// </summary>
public sealed record GuidedConversationData
{
    /// <summary>The conversation turns so far (alternating agent questions and user answers).</summary>
    public IReadOnlyList<ConversationTurn> Turns { get; init; } = [];

    /// <summary>The current question being asked by the agent (null if waiting for user input or complete).</summary>
    public string? CurrentQuestion { get; init; }

    /// <summary>The conversation phase.</summary>
    public ConversationPhase Phase { get; init; } = ConversationPhase.Ideation;

    /// <summary>The drafted specification text (populated in Drafting/Reviewing phases).</summary>
    public string? DraftedSpec { get; init; }

    /// <summary>Progress indicator: how many questions have been answered vs estimated total.</summary>
    public int QuestionsAnswered { get; init; }

    /// <summary>Estimated total questions for the current phase.</summary>
    public int EstimatedTotalQuestions { get; init; }
}

/// <summary>A single turn in the guided conversation.</summary>
public sealed record ConversationTurn
{
    /// <summary>Who sent this message.</summary>
    public ConversationRole Role { get; init; }

    /// <summary>The message content.</summary>
    public string Content { get; init; } = string.Empty;
}

/// <summary>The role of a conversation participant.</summary>
public enum ConversationRole
{
    /// <summary>The AI agent asking questions or providing analysis.</summary>
    Agent,
    /// <summary>The user providing answers or initial ideas.</summary>
    User,
}

/// <summary>Phases of the guided conversation flow.</summary>
public enum ConversationPhase
{
    /// <summary>Initial idea submission.</summary>
    Ideation,
    /// <summary>Structured interview gathering requirements.</summary>
    Interview,
    /// <summary>Agent is drafting the specification.</summary>
    Drafting,
    /// <summary>User is reviewing the drafted spec.</summary>
    Reviewing,
    /// <summary>Conversation complete, spec approved.</summary>
    Complete,
}
