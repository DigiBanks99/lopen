using System.Text.Json;

namespace Lopen.Core;

/// <summary>
/// Service for verifying task completion and quality.
/// Uses a dedicated sub-agent via Copilot SDK.
/// </summary>
public class VerificationService : IVerificationService
{
    private readonly ICopilotService _copilotService;
    private readonly string _workingDirectory;

    /// <summary>
    /// Creates a new VerificationService.
    /// </summary>
    public VerificationService(ICopilotService copilotService, string? workingDirectory = null)
    {
        _copilotService = copilotService;
        _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();
    }

    /// <inheritdoc />
    public async Task<VerificationResult> VerifyJobCompletionAsync(
        string jobId,
        string requirementCode,
        CancellationToken ct = default)
    {
        var prompt = BuildVerificationPrompt(jobId, requirementCode);

        await using var session = await _copilotService.CreateSessionAsync(
            new CopilotSessionOptions { Model = "gpt-5" }, ct);

        var response = await session.SendAsync(prompt, ct);

        return ParseVerificationResponse(response);
    }

    /// <inheritdoc />
    public async Task<VerificationResult> VerifyTestsAsync(string requirementCode, CancellationToken ct = default)
    {
        var prompt = $$"""
            Check if tests exist and pass for requirement {{requirementCode}}.
            Look for test files in the tests/ directory that test this requirement.
            Run `dotnet test` to verify tests pass.
            
            Respond with JSON:
            {"testsPass": true/false, "issues": ["list of issues if any"]}
            """;

        await using var session = await _copilotService.CreateSessionAsync(
            new CopilotSessionOptions { Model = "gpt-5" }, ct);

        var response = await session.SendAsync(prompt, ct);

        return ParseTestsResponse(response);
    }

    /// <inheritdoc />
    public async Task<VerificationResult> VerifyDocumentationAsync(string requirementCode, CancellationToken ct = default)
    {
        var prompt = $$"""
            Check if documentation exists for requirement {{requirementCode}}.
            Documentation should follow the Divio model (tutorial, how-to, reference, explanation).
            Look in docs/ directory for relevant documentation.
            
            Respond with JSON:
            {"documentationExists": true/false, "issues": ["list of issues if any"]}
            """;

        await using var session = await _copilotService.CreateSessionAsync(
            new CopilotSessionOptions { Model = "gpt-5" }, ct);

        var response = await session.SendAsync(prompt, ct);

        return ParseDocumentationResponse(response);
    }

    /// <inheritdoc />
    public async Task<VerificationResult> VerifyBuildAsync(CancellationToken ct = default)
    {
        var prompt = """
            Run `dotnet build` and verify the build succeeds.
            
            Respond with JSON:
            {"buildSucceeds": true/false, "issues": ["list of issues if any"]}
            """;

        await using var session = await _copilotService.CreateSessionAsync(
            new CopilotSessionOptions { Model = "gpt-5" }, ct);

        var response = await session.SendAsync(prompt, ct);

        return ParseBuildResponse(response);
    }

    /// <inheritdoc />
    public async Task<VerificationResult> VerifyRequirementCodeAsync(string requirementCode, CancellationToken ct = default)
    {
        // Local check first - no need for Copilot
        var isValid = await IsRequirementCodeValidAsync(requirementCode, ct);

        return isValid
            ? new VerificationResult { Complete = true, RequirementValid = true }
            : VerificationResult.Failed($"Requirement code {requirementCode} not found in any SPECIFICATION.md file");
    }

    private string BuildVerificationPrompt(string jobId, string requirementCode) =>
        $$"""
        You are a verification agent. Verify if the following task is complete:
        
        - Job ID: {{jobId}}
        - Requirement: {{requirementCode}}
        
        Check the following:
        1. Tests exist and pass for this requirement
        2. Documentation exists (Divio model)
        3. Build succeeds
        4. Requirement code is valid (exists in SPECIFICATION.md)
        5. Commits follow conventional commit format
        
        Respond in JSON format:
        {
            "complete": true/false,
            "testsPass": true/false,
            "documentationExists": true/false,
            "buildSucceeds": true/false,
            "requirementValid": true/false,
            "issues": ["list of any issues found"]
        }
        """;

    private VerificationResult ParseVerificationResponse(string? response)
    {
        if (string.IsNullOrEmpty(response))
            return VerificationResult.Failed("No response from verification agent");

        try
        {
            // Try to extract JSON from the response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..(jsonEnd + 1)];
                var result = JsonSerializer.Deserialize<VerificationJsonResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result != null)
                {
                    return new VerificationResult
                    {
                        Complete = result.Complete,
                        TestsPass = result.TestsPass,
                        DocumentationExists = result.DocumentationExists,
                        BuildSucceeds = result.BuildSucceeds,
                        RequirementValid = result.RequirementValid,
                        Issues = result.Issues ?? []
                    };
                }
            }
        }
        catch (JsonException)
        {
            // Fallback: parse text response
        }

        // Fallback: check for keywords
        return new VerificationResult
        {
            Complete = response.Contains("complete: true", StringComparison.OrdinalIgnoreCase) ||
                       response.Contains("\"complete\": true", StringComparison.OrdinalIgnoreCase),
            Issues = [response.Length > 500 ? response[..500] + "..." : response]
        };
    }

    private VerificationResult ParseTestsResponse(string? response)
    {
        if (string.IsNullOrEmpty(response))
            return VerificationResult.Failed("No response from verification agent");

        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..(jsonEnd + 1)];
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                var testsPass = data.TryGetProperty("testsPass", out var tp) && tp.GetBoolean();
                var issues = ExtractIssues(data);

                return new VerificationResult
                {
                    Complete = testsPass,
                    TestsPass = testsPass,
                    Issues = issues
                };
            }
        }
        catch (JsonException)
        {
        }

        return VerificationResult.Failed("Could not parse tests verification response");
    }

    private VerificationResult ParseDocumentationResponse(string? response)
    {
        if (string.IsNullOrEmpty(response))
            return VerificationResult.Failed("No response from verification agent");

        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..(jsonEnd + 1)];
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                var docExists = data.TryGetProperty("documentationExists", out var de) && de.GetBoolean();
                var issues = ExtractIssues(data);

                return new VerificationResult
                {
                    Complete = docExists,
                    DocumentationExists = docExists,
                    Issues = issues
                };
            }
        }
        catch (JsonException)
        {
        }

        return VerificationResult.Failed("Could not parse documentation verification response");
    }

    private VerificationResult ParseBuildResponse(string? response)
    {
        if (string.IsNullOrEmpty(response))
            return VerificationResult.Failed("No response from verification agent");

        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response[jsonStart..(jsonEnd + 1)];
                var data = JsonSerializer.Deserialize<JsonElement>(json);

                var buildSucceeds = data.TryGetProperty("buildSucceeds", out var bs) && bs.GetBoolean();
                var issues = ExtractIssues(data);

                return new VerificationResult
                {
                    Complete = buildSucceeds,
                    BuildSucceeds = buildSucceeds,
                    Issues = issues
                };
            }
        }
        catch (JsonException)
        {
        }

        return VerificationResult.Failed("Could not parse build verification response");
    }

    private static List<string> ExtractIssues(JsonElement data)
    {
        if (data.TryGetProperty("issues", out var issuesElement) && issuesElement.ValueKind == JsonValueKind.Array)
        {
            return issuesElement.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        return [];
    }

    private async Task<bool> IsRequirementCodeValidAsync(string requirementCode, CancellationToken ct)
    {
        var docsPath = Path.Combine(_workingDirectory, "docs", "requirements");

        if (!Directory.Exists(docsPath))
            return false;

        var specFiles = Directory.GetFiles(docsPath, "SPECIFICATION.md", SearchOption.AllDirectories);

        foreach (var file in specFiles)
        {
            ct.ThrowIfCancellationRequested();

            var content = await File.ReadAllTextAsync(file, ct);
            if (content.Contains(requirementCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private class VerificationJsonResponse
    {
        public bool Complete { get; set; }
        public bool TestsPass { get; set; }
        public bool DocumentationExists { get; set; }
        public bool BuildSucceeds { get; set; }
        public bool RequirementValid { get; set; }
        public List<string>? Issues { get; set; }
    }
}
