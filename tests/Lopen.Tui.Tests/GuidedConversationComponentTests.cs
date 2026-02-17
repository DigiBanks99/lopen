using Lopen.Tui;

namespace Lopen.Tui.Tests;

public class GuidedConversationComponentTests
{
    private readonly GuidedConversationComponent _sut = new();
    private readonly ScreenRect _region = new(0, 0, 60, 20);

    // --- Basic rendering ---

    [Fact]
    public void Render_EmptyRegion_ReturnsEmpty()
    {
        var data = new GuidedConversationData();
        var result = _sut.Render(data, new ScreenRect(0, 0, 0, 0));
        Assert.Empty(result);
    }

    [Fact]
    public void Render_ZeroWidth_ReturnsEmpty()
    {
        var data = new GuidedConversationData();
        var result = _sut.Render(data, new ScreenRect(0, 0, 0, 10));
        Assert.Empty(result);
    }

    [Fact]
    public void Render_DefaultData_ShowsIdeationHeader()
    {
        var data = new GuidedConversationData();
        var result = _sut.Render(data, _region);

        Assert.Equal(_region.Height, result.Length);
        Assert.Contains("Ideation", result[0]);
    }

    [Fact]
    public void Render_ExactHeight_AllLinesPaddedToWidth()
    {
        var data = new GuidedConversationData();
        var result = _sut.Render(data, _region);

        Assert.Equal(_region.Height, result.Length);
        foreach (var line in result)
            Assert.Equal(_region.Width, line.Length);
    }

    // --- Phase headers ---

    [Theory]
    [InlineData(ConversationPhase.Ideation, "Ideation")]
    [InlineData(ConversationPhase.Interview, "Interview")]
    [InlineData(ConversationPhase.Drafting, "Drafting")]
    [InlineData(ConversationPhase.Reviewing, "Review")]
    [InlineData(ConversationPhase.Complete, "Complete")]
    public void Render_PhaseHeader_MatchesPhase(ConversationPhase phase, string expectedText)
    {
        var data = new GuidedConversationData { Phase = phase };
        var result = _sut.Render(data, _region);

        Assert.Contains(expectedText, result[0]);
    }

    // --- Progress indicator ---

    [Fact]
    public void Render_WithEstimatedQuestions_ShowsProgress()
    {
        var data = new GuidedConversationData
        {
            QuestionsAnswered = 3,
            EstimatedTotalQuestions = 8,
        };
        var result = _sut.Render(data, _region);

        Assert.Contains(result, l => l.TrimEnd().Contains("[3/8]"));
    }

    [Fact]
    public void Render_ZeroEstimatedQuestions_NoProgressLine()
    {
        var data = new GuidedConversationData { EstimatedTotalQuestions = 0 };
        var result = _sut.Render(data, _region);

        Assert.DoesNotContain(result, l => l.Contains("[0/0]"));
    }

    // --- Conversation turns ---

    [Fact]
    public void Render_AgentTurn_PrefixedWithQuestionMark()
    {
        var data = new GuidedConversationData
        {
            Turns = [new() { Role = ConversationRole.Agent, Content = "What is your goal?" }],
        };
        var result = _sut.Render(data, _region);

        Assert.Contains(result, l => l.TrimEnd().StartsWith("? What is your goal?"));
    }

    [Fact]
    public void Render_UserTurn_PrefixedWithAngleBracket()
    {
        var data = new GuidedConversationData
        {
            Turns = [new() { Role = ConversationRole.User, Content = "Build a REST API" }],
        };
        var result = _sut.Render(data, _region);

        Assert.Contains(result, l => l.TrimEnd().StartsWith("> Build a REST API"));
    }

    [Fact]
    public void Render_MultipleTurns_AllRendered()
    {
        var data = new GuidedConversationData
        {
            Turns = [
                new() { Role = ConversationRole.Agent, Content = "Question one?" },
                new() { Role = ConversationRole.User, Content = "Answer one." },
                new() { Role = ConversationRole.Agent, Content = "Question two?" },
                new() { Role = ConversationRole.User, Content = "Answer two." },
            ],
        };
        var result = _sut.Render(data, new ScreenRect(0, 0, 60, 30));
        var joined = string.Join("\n", result);

        Assert.Contains("? Question one?", joined);
        Assert.Contains("> Answer one.", joined);
        Assert.Contains("? Question two?", joined);
        Assert.Contains("> Answer two.", joined);
    }

    // --- Current question ---

    [Fact]
    public void Render_CurrentQuestion_ShownWithPrefix()
    {
        var data = new GuidedConversationData
        {
            CurrentQuestion = "What are the constraints?",
        };
        var result = _sut.Render(data, _region);
        var joined = string.Join("\n", result);

        Assert.Contains("? What are the constraints?", joined);
    }

    [Fact]
    public void Render_NullCurrentQuestion_NoExtraQuestionLine()
    {
        var data = new GuidedConversationData { CurrentQuestion = null };
        var result = _sut.Render(data, _region);

        // Only the header line should start with a non-space character
        var nonEmptyLines = result.Where(l => l.Trim().Length > 0).ToArray();
        Assert.Single(nonEmptyLines); // Just the phase header
    }

    // --- Drafted spec ---

    [Fact]
    public void Render_DraftedSpec_ShowsSpecSection()
    {
        var data = new GuidedConversationData
        {
            Phase = ConversationPhase.Reviewing,
            DraftedSpec = "# Auth Module\n\nOAuth 2.0 with JWT.",
        };
        var result = _sut.Render(data, new ScreenRect(0, 0, 60, 30));
        var joined = string.Join("\n", result);

        Assert.Contains("Drafted Specification", joined);
        Assert.Contains("# Auth Module", joined);
        Assert.Contains("OAuth 2.0 with JWT.", joined);
    }

    [Fact]
    public void Render_NoDraftedSpec_NoSpecSection()
    {
        var data = new GuidedConversationData();
        var result = _sut.Render(data, _region);
        var joined = string.Join("\n", result);

        Assert.DoesNotContain("Drafted Specification", joined);
    }

    // --- Preview states ---

    [Fact]
    public void GetPreviewStates_ReturnsFourStates()
    {
        var states = _sut.GetPreviewStates();
        Assert.Equal(4, states.Count);
        Assert.Contains("empty", states);
        Assert.Contains("populated", states);
        Assert.Contains("error", states);
        Assert.Contains("loading", states);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("populated")]
    [InlineData("error")]
    [InlineData("loading")]
    public void RenderPreview_AllStates_ReturnNonEmptyOutput(string state)
    {
        var result = _sut.RenderPreview(state, 60, 20);
        Assert.Equal(20, result.Length);
        Assert.Contains(result, l => l.Trim().Length > 0);
    }

    [Fact]
    public void RenderPreview_Default_ReturnsPopulated()
    {
        var result = _sut.RenderPreview(60, 20);
        Assert.Equal(20, result.Length);
        var joined = string.Join("\n", result);
        Assert.Contains("Interview", joined);
    }

    // --- ITuiComponent ---

    [Fact]
    public void Name_IsGuidedConversation()
    {
        Assert.Equal("GuidedConversation", _sut.Name);
    }

    [Fact]
    public void Description_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(_sut.Description));
    }

    // --- Text wrapping ---

    [Fact]
    public void Render_LongContent_WrapsToWidth()
    {
        var longText = new string('x', 100);
        var data = new GuidedConversationData
        {
            Turns = [new() { Role = ConversationRole.User, Content = longText }],
        };
        var result = _sut.Render(data, new ScreenRect(0, 0, 40, 20));

        // All lines should be exactly 40 chars
        foreach (var line in result)
            Assert.Equal(40, line.Length);
    }

    // --- Data model ---

    [Fact]
    public void ConversationTurn_DefaultContent_IsEmpty()
    {
        var turn = new ConversationTurn();
        Assert.Equal(string.Empty, turn.Content);
    }

    [Fact]
    public void GuidedConversationData_DefaultTurns_IsEmpty()
    {
        var data = new GuidedConversationData();
        Assert.Empty(data.Turns);
    }

    [Fact]
    public void GuidedConversationData_DefaultPhase_IsIdeation()
    {
        var data = new GuidedConversationData();
        Assert.Equal(ConversationPhase.Ideation, data.Phase);
    }
}
