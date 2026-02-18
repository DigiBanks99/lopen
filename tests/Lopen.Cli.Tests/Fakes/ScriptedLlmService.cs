using Lopen.Llm;

namespace Lopen.Cli.Tests.Fakes;

/// <summary>
/// Test fake that returns scripted LLM responses in sequence.
/// Used for E2E integration tests that exercise the real WorkflowOrchestrator
/// with deterministic, pre-recorded responses.
/// </summary>
public sealed class ScriptedLlmService : ILlmService
{
    private readonly Queue<LlmInvocationResult> _responses;
    private readonly LlmInvocationResult _defaultResponse;

    public List<(string SystemPrompt, string Model, IReadOnlyList<LopenToolDefinition> Tools)> Invocations { get; } = [];
    public int InvokeCount => Invocations.Count;

    public ScriptedLlmService(IEnumerable<LlmInvocationResult> responses)
    {
        _responses = new Queue<LlmInvocationResult>(responses);
        _defaultResponse = new LlmInvocationResult(
            "Scripted response (default)",
            new TokenUsage(100, 50, 150, 8000, false),
            1,
            true);
    }

    public ScriptedLlmService(params LlmInvocationResult[] responses)
        : this((IEnumerable<LlmInvocationResult>)responses) { }

    public Task<LlmInvocationResult> InvokeAsync(
        string systemPrompt,
        string model,
        IReadOnlyList<LopenToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        Invocations.Add((systemPrompt, model, tools));
        var response = _responses.Count > 0 ? _responses.Dequeue() : _defaultResponse;
        return Task.FromResult(response);
    }

    public static LlmInvocationResult CreateResponse(string output = "Test output", int toolCalls = 1, bool isComplete = true)
        => new(output, new TokenUsage(100, 50, 150, 8000, false), toolCalls, isComplete);
}
