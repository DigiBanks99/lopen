using GitHub.Copilot.SDK;
using Microsoft.Extensions.Logging;

namespace Lopen.Llm;

/// <summary>
/// LLM service implementation using the GitHub Copilot SDK.
/// Each invocation creates a fresh session with no conversation history.
/// </summary>
internal sealed class CopilotLlmService : ILlmService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    private readonly ICopilotClientProvider _clientProvider;
    private readonly IAuthErrorHandler _authErrorHandler;
    private readonly ILogger<CopilotLlmService> _logger;

    public CopilotLlmService(
        ICopilotClientProvider clientProvider,
        IAuthErrorHandler authErrorHandler,
        ILogger<CopilotLlmService> logger)
    {
        _clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
        _authErrorHandler = authErrorHandler ?? throw new ArgumentNullException(nameof(authErrorHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LlmInvocationResult> InvokeAsync(
        string systemPrompt,
        string model,
        IReadOnlyList<LopenToolDefinition> tools,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(tools);

        _logger.LogInformation("Invoking Copilot SDK with model {Model}, {ToolCount} tools", model, tools.Count);

        CopilotClient client;
        try
        {
            client = await _clientProvider.GetClientAsync(cancellationToken);
        }
        catch (LlmException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LlmException("Failed to get Copilot client", model, ex);
        }

        // Verify auth before creating session
        var authStatus = await client.GetAuthStatusAsync(cancellationToken);
        if (!authStatus.IsAuthenticated)
        {
            throw new LlmException(
                $"Copilot SDK authentication failed: {authStatus.StatusMessage ?? "not authenticated"}",
                model);
        }

        var aiFunctions = ToolConversion.ToAiFunctions(tools);
        _logger.LogDebug("Converted {BoundToolCount}/{TotalToolCount} tools to AIFunction instances",
            aiFunctions.Count, tools.Count);

        var config = new SessionConfig
        {
            Model = model,
            SystemMessage = new SystemMessageConfig
            {
                Content = systemPrompt,
                Mode = SystemMessageMode.Replace,
            },
            Tools = aiFunctions,
            Streaming = false,
            Hooks = new SessionHooks
            {
                OnErrorOccurred = async (input, _) =>
                {
                    var result = await _authErrorHandler.HandleErrorAsync(input, cancellationToken);
                    return result ?? new ErrorOccurredHookOutput();
                },
            },
        };

        // Track usage via events
        int inputTokens = 0, outputTokens = 0, toolCallCount = 0;
        int contextWindowSize = 0;

        await using var session = await client.CreateSessionAsync(config, cancellationToken);

        using var eventSub = session.On(evt =>
        {
            switch (evt)
            {
                case AssistantUsageEvent usage:
                    inputTokens += (int)(usage.Data.InputTokens ?? 0);
                    outputTokens += (int)(usage.Data.OutputTokens ?? 0);
                    break;
                case ToolExecutionCompleteEvent:
                    Interlocked.Increment(ref toolCallCount);
                    break;
                case SessionUsageInfoEvent usageInfo:
                    contextWindowSize = (int)usageInfo.Data.TokenLimit;
                    break;
                case SessionErrorEvent error:
                    _logger.LogWarning("Session error: {ErrorType} - {Message}",
                        error.Data.ErrorType, error.Data.Message);
                    break;
            }
        });

        try
        {
            _logger.LogDebug("Sending prompt to session {SessionId}", session.SessionId);

            var response = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = "" },
                DefaultTimeout,
                cancellationToken);

            var output = response?.Data?.Content ?? string.Empty;

            _logger.LogInformation(
                "SDK invocation complete: {InputTokens} in, {OutputTokens} out, {ToolCalls} tool calls",
                inputTokens, outputTokens, toolCallCount);

            return new LlmInvocationResult(
                Output: output,
                TokenUsage: new TokenUsage(
                    InputTokens: inputTokens,
                    OutputTokens: outputTokens,
                    TotalTokens: inputTokens + outputTokens,
                    ContextWindowSize: contextWindowSize,
                    IsPremiumRequest: IsPremiumModel(model)),
                ToolCallsMade: toolCallCount,
                IsComplete: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not LlmException)
        {
            _logger.LogError(ex, "SDK invocation failed for model {Model}", model);
            throw new LlmException($"SDK invocation failed: {ex.Message}", model, ex);
        }
    }

    /// <summary>
    /// Determines if a model is a premium model (consumes premium API requests).
    /// Premium models include Claude Opus, GPT-5, and other flagship models.
    /// </summary>
    internal static bool IsPremiumModel(string model)
    {
        // gpt-5-mini and similar -mini variants are standard-tier, not premium
        if (model.Contains("-mini", StringComparison.OrdinalIgnoreCase))
            return false;

        return model.Contains("opus", StringComparison.OrdinalIgnoreCase)
            || model.Contains("gpt-5", StringComparison.OrdinalIgnoreCase)
            || model.Contains("o3", StringComparison.OrdinalIgnoreCase)
            || model.Contains("o1", StringComparison.OrdinalIgnoreCase);
    }
}
