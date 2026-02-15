using Microsoft.Extensions.Logging;

namespace Lopen.Llm;

/// <summary>
/// Stub LLM service that throws until the Copilot SDK integration is implemented.
/// </summary>
internal sealed class StubLlmService : ILlmService
{
    private readonly ILogger<StubLlmService> _logger;

    public StubLlmService(ILogger<StubLlmService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<LlmInvocationResult> InvokeAsync(
        string systemPrompt,
        string model,
        IReadOnlyList<LopenToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Copilot SDK integration is not yet available");
        throw new LlmException("Copilot SDK integration pending", model);
    }
}
