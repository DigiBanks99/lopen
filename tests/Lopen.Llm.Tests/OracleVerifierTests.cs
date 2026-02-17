using Lopen.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Llm.Tests;

public sealed class OracleVerifierTests
{
    private const string SampleEvidence = "Added login endpoint with JWT validation. Tests pass.";
    private const string SampleCriteria = "- User can authenticate with username and password\n- JWT token is returned on success";

    private static OracleVerifier CreateVerifier(
        ILlmService? llmService = null,
        OracleOptions? options = null)
    {
        return new OracleVerifier(
            llmService ?? new FakeLlmService("""{"pass": true, "gaps": []}"""),
            options ?? new OracleOptions(),
            NullLogger<OracleVerifier>.Instance);
    }

    // --- Constructor null guards ---

    [Fact]
    public void Constructor_NullLlmService_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OracleVerifier(null!, new OracleOptions(), NullLogger<OracleVerifier>.Instance));
    }

    [Fact]
    public void Constructor_NullOracleOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OracleVerifier(new FakeLlmService("{}"), null!, NullLogger<OracleVerifier>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new OracleVerifier(new FakeLlmService("{}"), new OracleOptions(), null!));
    }

    // --- VerifyAsync argument validation ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyAsync_EmptyOrWhitespaceEvidence_Throws(string evidence)
    {
        var verifier = CreateVerifier();

        await Assert.ThrowsAsync<ArgumentException>(
            () => verifier.VerifyAsync(VerificationScope.Task, evidence, SampleCriteria));
    }

    [Fact]
    public async Task VerifyAsync_NullEvidence_Throws()
    {
        var verifier = CreateVerifier();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => verifier.VerifyAsync(VerificationScope.Task, null!, SampleCriteria));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyAsync_EmptyOrWhitespaceAcceptanceCriteria_Throws(string criteria)
    {
        var verifier = CreateVerifier();

        await Assert.ThrowsAsync<ArgumentException>(
            () => verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, criteria));
    }

    [Fact]
    public async Task VerifyAsync_NullAcceptanceCriteria_Throws()
    {
        var verifier = CreateVerifier();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, null!));
    }

    // --- Passing verification ---

    [Fact]
    public async Task VerifyAsync_OracleReturnsPassing_VerdictIsPass()
    {
        var llm = new FakeLlmService("""{"pass": true, "gaps": []}""");
        var verifier = CreateVerifier(llm);

        var verdict = await verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, SampleCriteria);

        Assert.True(verdict.Passed);
        Assert.Empty(verdict.Gaps);
        Assert.Equal(VerificationScope.Task, verdict.Scope);
    }

    [Theory]
    [InlineData(VerificationScope.Task)]
    [InlineData(VerificationScope.Component)]
    [InlineData(VerificationScope.Module)]
    public async Task VerifyAsync_AllScopes_PreservedInVerdict(VerificationScope scope)
    {
        var llm = new FakeLlmService("""{"pass": true, "gaps": []}""");
        var verifier = CreateVerifier(llm);

        var verdict = await verifier.VerifyAsync(scope, SampleEvidence, SampleCriteria);

        Assert.Equal(scope, verdict.Scope);
    }

    // --- Failing verification ---

    [Fact]
    public async Task VerifyAsync_OracleReturnsFailing_VerdictIsFailWithGaps()
    {
        var llm = new FakeLlmService("""{"pass": false, "gaps": ["Missing test for login", "No error handling"]}""");
        var verifier = CreateVerifier(llm);

        var verdict = await verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, SampleCriteria);

        Assert.False(verdict.Passed);
        Assert.Equal(2, verdict.Gaps.Count);
        Assert.Contains("Missing test for login", verdict.Gaps);
        Assert.Contains("No error handling", verdict.Gaps);
    }

    [Fact]
    public async Task VerifyAsync_OraclePassTrueButHasGaps_VerdictIsFail()
    {
        // Even if oracle says pass=true, gaps presence means failure
        var llm = new FakeLlmService("""{"pass": true, "gaps": ["Something missed"]}""");
        var verifier = CreateVerifier(llm);

        var verdict = await verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, SampleCriteria);

        Assert.False(verdict.Passed);
        Assert.Single(verdict.Gaps);
    }

    // --- Oracle model configuration ---

    [Fact]
    public async Task VerifyAsync_UsesConfiguredOracleModel()
    {
        var llm = new FakeLlmService("""{"pass": true, "gaps": []}""");
        var options = new OracleOptions { Model = "gpt-4.1" };
        var verifier = CreateVerifier(llm, options);

        await verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, SampleCriteria);

        Assert.Equal("gpt-4.1", llm.LastModelUsed);
    }

    [Fact]
    public async Task VerifyAsync_DefaultOracleModel_IsGpt5Mini()
    {
        var llm = new FakeLlmService("""{"pass": true, "gaps": []}""");
        var verifier = CreateVerifier(llm);

        await verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, SampleCriteria);

        Assert.Equal("gpt-5-mini", llm.LastModelUsed);
    }

    [Fact]
    public async Task VerifyAsync_NoToolsRegistered()
    {
        var llm = new FakeLlmService("""{"pass": true, "gaps": []}""");
        var verifier = CreateVerifier(llm);

        await verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, SampleCriteria);

        Assert.NotNull(llm.LastToolsUsed);
        Assert.Empty(llm.LastToolsUsed);
    }

    // --- Error handling ---

    [Fact]
    public async Task VerifyAsync_LlmThrows_ReturnsFailVerdictWithMessage()
    {
        var llm = new ThrowingLlmService("SDK connection failed");
        var verifier = CreateVerifier(llm);

        var verdict = await verifier.VerifyAsync(VerificationScope.Component, SampleEvidence, SampleCriteria);

        Assert.False(verdict.Passed);
        Assert.Single(verdict.Gaps);
        Assert.Contains("SDK connection failed", verdict.Gaps[0]);
        Assert.Equal(VerificationScope.Component, verdict.Scope);
    }

    [Fact]
    public async Task VerifyAsync_EmptyResponse_ReturnsFailVerdict()
    {
        var llm = new FakeLlmService("");
        var verifier = CreateVerifier(llm);

        var verdict = await verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, SampleCriteria);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Gaps, g => g.Contains("empty response"));
    }

    [Fact]
    public async Task VerifyAsync_InvalidJson_ReturnsFailVerdict()
    {
        var llm = new FakeLlmService("This is not JSON at all");
        var verifier = CreateVerifier(llm);

        var verdict = await verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, SampleCriteria);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Gaps, g => g.Contains("not valid JSON"));
    }

    // --- JSON extraction ---

    [Fact]
    public async Task VerifyAsync_JsonInMarkdownCodeFence_ParsesCorrectly()
    {
        var llm = new FakeLlmService("```json\n{\"pass\": true, \"gaps\": []}\n```");
        var verifier = CreateVerifier(llm);

        var verdict = await verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, SampleCriteria);

        Assert.True(verdict.Passed);
    }

    [Fact]
    public async Task VerifyAsync_JsonWithSurroundingText_ParsesCorrectly()
    {
        var llm = new FakeLlmService("Here is my analysis:\n{\"pass\": false, \"gaps\": [\"No tests\"]}\nEnd.");
        var verifier = CreateVerifier(llm);

        var verdict = await verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, SampleCriteria);

        Assert.False(verdict.Passed);
        Assert.Single(verdict.Gaps);
        Assert.Equal("No tests", verdict.Gaps[0]);
    }

    // --- BuildPrompt tests ---

    [Fact]
    public void BuildPrompt_IncludesAcceptanceCriteria()
    {
        var prompt = OracleVerifier.BuildPrompt(VerificationScope.Task, "diff output", "- AC1\n- AC2");

        Assert.Contains("- AC1", prompt);
        Assert.Contains("- AC2", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesEvidence()
    {
        var prompt = OracleVerifier.BuildPrompt(VerificationScope.Task, "diff output here", "- AC1");

        Assert.Contains("diff output here", prompt);
    }

    [Theory]
    [InlineData(VerificationScope.Task, "task")]
    [InlineData(VerificationScope.Component, "component")]
    [InlineData(VerificationScope.Module, "module")]
    public void BuildPrompt_IncludesScopeLabel(VerificationScope scope, string expectedLabel)
    {
        var prompt = OracleVerifier.BuildPrompt(scope, "evidence", "criteria");

        Assert.Contains(expectedLabel, prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesJsonFormatInstruction()
    {
        var prompt = OracleVerifier.BuildPrompt(VerificationScope.Task, "evidence", "criteria");

        Assert.Contains("\"pass\"", prompt);
        Assert.Contains("\"gaps\"", prompt);
    }

    // --- ParseVerdict tests ---

    [Fact]
    public void ParseVerdict_NullOutput_ReturnsFailure()
    {
        var verdict = OracleVerifier.ParseVerdict(null, VerificationScope.Task);

        Assert.False(verdict.Passed);
        Assert.Contains(verdict.Gaps, g => g.Contains("empty response"));
    }

    [Fact]
    public void ParseVerdict_ValidPassingJson_ReturnsPassed()
    {
        var verdict = OracleVerifier.ParseVerdict("""{"pass": true, "gaps": []}""", VerificationScope.Task);

        Assert.True(verdict.Passed);
        Assert.Empty(verdict.Gaps);
    }

    [Fact]
    public void ParseVerdict_ValidFailingJson_ReturnsFailed()
    {
        var verdict = OracleVerifier.ParseVerdict(
            """{"pass": false, "gaps": ["gap1"]}""", VerificationScope.Component);

        Assert.False(verdict.Passed);
        Assert.Single(verdict.Gaps);
        Assert.Equal(VerificationScope.Component, verdict.Scope);
    }

    // --- ExtractJson tests ---

    [Fact]
    public void ExtractJson_PlainJson_ReturnsAsIs()
    {
        var result = OracleVerifier.ExtractJson("""{"pass": true}""");
        Assert.Equal("""{"pass": true}""", result);
    }

    [Fact]
    public void ExtractJson_MarkdownFence_StripsWrapper()
    {
        var result = OracleVerifier.ExtractJson("```json\n{\"pass\": true}\n```");
        Assert.Equal("{\"pass\": true}", result);
    }

    [Fact]
    public void ExtractJson_TextAroundJson_ExtractsBraces()
    {
        var result = OracleVerifier.ExtractJson("Here: {\"pass\": true} done");
        Assert.Equal("{\"pass\": true}", result);
    }

    // --- Cancellation ---

    [Fact]
    public async Task VerifyAsync_CancellationRequested_ThrowsOperationCanceled()
    {
        var llm = new CancellingLlmService();
        var verifier = CreateVerifier(llm);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => verifier.VerifyAsync(VerificationScope.Task, SampleEvidence, SampleCriteria, cts.Token));
    }

    // --- Test doubles ---

    private sealed class FakeLlmService : ILlmService
    {
        private readonly string _output;

        public string? LastModelUsed { get; private set; }
        public IReadOnlyList<LopenToolDefinition>? LastToolsUsed { get; private set; }
        public string? LastPromptUsed { get; private set; }

        public FakeLlmService(string output) => _output = output;

        public Task<LlmInvocationResult> InvokeAsync(
            string systemPrompt,
            string model,
            IReadOnlyList<LopenToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            LastPromptUsed = systemPrompt;
            LastModelUsed = model;
            LastToolsUsed = tools;

            return Task.FromResult(new LlmInvocationResult(
                Output: _output,
                TokenUsage: new TokenUsage(100, 50, 150, 8192, false),
                ToolCallsMade: 0,
                IsComplete: true));
        }
    }

    private sealed class ThrowingLlmService : ILlmService
    {
        private readonly string _message;

        public ThrowingLlmService(string message) => _message = message;

        public Task<LlmInvocationResult> InvokeAsync(
            string systemPrompt,
            string model,
            IReadOnlyList<LopenToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            throw new LlmException(_message);
        }
    }

    private sealed class CancellingLlmService : ILlmService
    {
        public Task<LlmInvocationResult> InvokeAsync(
            string systemPrompt,
            string model,
            IReadOnlyList<LopenToolDefinition> tools,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new LlmInvocationResult("", new TokenUsage(0, 0, 0, 0, false), 0, true));
        }
    }
}
