using System.Text.Json;
using Lopen.Configuration;
using Microsoft.Extensions.Logging;

namespace Lopen.Llm;

/// <summary>
/// Dispatches a cheap/fast oracle sub-agent via ILlmService to verify
/// that work at a given scope meets its acceptance criteria.
/// </summary>
internal sealed class OracleVerifier : IOracleVerifier
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILlmService _llmService;
    private readonly OracleOptions _oracleOptions;
    private readonly ILogger<OracleVerifier> _logger;

    public OracleVerifier(
        ILlmService llmService,
        OracleOptions oracleOptions,
        ILogger<OracleVerifier> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _oracleOptions = oracleOptions ?? throw new ArgumentNullException(nameof(oracleOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OracleVerdict> VerifyAsync(
        VerificationScope scope,
        string evidence,
        string acceptanceCriteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidence);
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptanceCriteria);

        _logger.LogInformation(
            "Dispatching oracle verification for scope {Scope} using model {Model}",
            scope, _oracleOptions.Model);

        var prompt = BuildPrompt(scope, evidence, acceptanceCriteria);

        LlmInvocationResult result;
        try
        {
            result = await _llmService.InvokeAsync(
                prompt,
                _oracleOptions.Model,
                Array.Empty<LopenToolDefinition>(),
                cancellationToken);
        }
        catch (LlmException ex)
        {
            _logger.LogError(ex, "Oracle invocation failed for scope {Scope}", scope);
            return new OracleVerdict(
                Passed: false,
                Gaps: [$"Oracle invocation failed: {ex.Message}"],
                Scope: scope);
        }

        var verdict = ParseVerdict(result.Output, scope);

        _logger.LogInformation(
            "Oracle verdict for scope {Scope}: Passed={Passed}, Gaps={GapCount}",
            scope, verdict.Passed, verdict.Gaps.Count);

        return verdict;
    }

    internal static string BuildPrompt(
        VerificationScope scope,
        string evidence,
        string acceptanceCriteria)
    {
        var scopeLabel = scope switch
        {
            VerificationScope.Task => "task",
            VerificationScope.Component => "component",
            VerificationScope.Module => "module",
            _ => scope.ToString().ToLowerInvariant(),
        };

        return $$"""
            You are a verification oracle. Your job is to review evidence and determine whether the {{scopeLabel}} meets its acceptance criteria.

            Rules:
            - Be strict: every acceptance criterion must be met
            - If any criterion is not satisfied, the verification fails
            - List each unmet criterion as a separate gap
            - Respond ONLY with a JSON object in this exact format: {"pass": true/false, "gaps": ["gap1", "gap2"]}
            - Do not include any text outside the JSON object

            ## Acceptance Criteria

            {{acceptanceCriteria}}

            ## Evidence

            {{evidence}}
            """;
    }

    internal static OracleVerdict ParseVerdict(string? output, VerificationScope scope)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new OracleVerdict(
                Passed: false,
                Gaps: ["Oracle returned empty response"],
                Scope: scope);
        }

        // Try to extract JSON from the response (may contain markdown fences)
        var json = ExtractJson(output);

        try
        {
            var parsed = JsonSerializer.Deserialize<OracleResponseDto>(json, JsonOptions);
            if (parsed is null)
            {
                return new OracleVerdict(
                    Passed: false,
                    Gaps: ["Oracle response could not be parsed"],
                    Scope: scope);
            }

            var gaps = parsed.Gaps ?? [];
            return new OracleVerdict(
                Passed: parsed.Pass && gaps.Count == 0,
                Gaps: gaps,
                Scope: scope);
        }
        catch (JsonException)
        {
            return new OracleVerdict(
                Passed: false,
                Gaps: [$"Oracle response was not valid JSON: {output[..Math.Min(output.Length, 200)]}"],
                Scope: scope);
        }
    }

    internal static string ExtractJson(string text)
    {
        // Strip markdown code fences if present
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
            {
                trimmed = trimmed[(firstNewline + 1)..];
            }

            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0)
            {
                trimmed = trimmed[..lastFence];
            }
        }

        // Find the JSON object boundaries
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            return trimmed[start..(end + 1)];
        }

        return trimmed;
    }

    private sealed class OracleResponseDto
    {
        public bool Pass { get; set; }
        public List<string>? Gaps { get; set; }
    }
}
