using Shouldly;

namespace Lopen.Core.Tests;

public class VerificationServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly MockCopilotService _mockCopilotService;
    private readonly VerificationService _service;

    public VerificationServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"lopen-verify-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        // Create requirements structure
        var docsPath = Path.Combine(_testDir, "docs", "requirements", "loop");
        Directory.CreateDirectory(docsPath);
        File.WriteAllText(Path.Combine(docsPath, "SPECIFICATION.md"), """
            # Loop Specification
            | ID | Requirement |
            | REQ-036 | Verification Agent |
            | REQ-030 | Loop Command |
            """);

        _mockCopilotService = new MockCopilotService();
        _service = new VerificationService(_mockCopilotService, _testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyJobCompletionAsync_CreatesSession()
    {
        var result = await _service.VerifyJobCompletionAsync("JTBD-025", "REQ-036");

        _mockCopilotService.SessionsCreated.ShouldBe(1);
    }

    [Fact]
    public async Task VerifyJobCompletionAsync_ParsesCompleteResponse()
    {
        _mockCopilotService.SetResponse("""
            {
                "complete": true,
                "testsPass": true,
                "documentationExists": true,
                "buildSucceeds": true,
                "requirementValid": true,
                "issues": []
            }
            """);

        var result = await _service.VerifyJobCompletionAsync("JTBD-025", "REQ-036");

        result.Complete.ShouldBeTrue();
        result.TestsPass.ShouldBeTrue();
        result.DocumentationExists.ShouldBeTrue();
        result.BuildSucceeds.ShouldBeTrue();
        result.RequirementValid.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public async Task VerifyJobCompletionAsync_ParsesIncompleteResponse()
    {
        _mockCopilotService.SetResponse("""
            {
                "complete": false,
                "testsPass": false,
                "documentationExists": true,
                "buildSucceeds": true,
                "requirementValid": true,
                "issues": ["Tests are missing"]
            }
            """);

        var result = await _service.VerifyJobCompletionAsync("JTBD-025", "REQ-036");

        result.Complete.ShouldBeFalse();
        result.TestsPass.ShouldBeFalse();
        result.Issues.ShouldContain("Tests are missing");
    }

    [Fact]
    public async Task VerifyJobCompletionAsync_HandlesEmptyResponse()
    {
        _mockCopilotService.SetResponse("");

        var result = await _service.VerifyJobCompletionAsync("JTBD-025", "REQ-036");

        result.Complete.ShouldBeFalse();
        result.Issues.ShouldContain("No response from verification agent");
    }

    [Fact]
    public async Task VerifyJobCompletionAsync_HandlesNullResponse()
    {
        _mockCopilotService.SetResponse(null);

        var result = await _service.VerifyJobCompletionAsync("JTBD-025", "REQ-036");

        result.Complete.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyJobCompletionAsync_HandlesJsonInText()
    {
        _mockCopilotService.SetResponse("""
            Here is my verification:
            {"complete": true, "testsPass": true, "documentationExists": true, "buildSucceeds": true, "requirementValid": true, "issues": []}
            That's my analysis.
            """);

        var result = await _service.VerifyJobCompletionAsync("JTBD-025", "REQ-036");

        result.Complete.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyTestsAsync_ParsesTestsPassResponse()
    {
        _mockCopilotService.SetResponse("""{"testsPass": true, "issues": []}""");

        var result = await _service.VerifyTestsAsync("REQ-036");

        result.TestsPass.ShouldBeTrue();
        result.Complete.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyTestsAsync_ParsesTestsFailResponse()
    {
        _mockCopilotService.SetResponse("""{"testsPass": false, "issues": ["No tests found"]}""");

        var result = await _service.VerifyTestsAsync("REQ-036");

        result.TestsPass.ShouldBeFalse();
        result.Issues.ShouldContain("No tests found");
    }

    [Fact]
    public async Task VerifyDocumentationAsync_ParsesDocExistsResponse()
    {
        _mockCopilotService.SetResponse("""{"documentationExists": true, "issues": []}""");

        var result = await _service.VerifyDocumentationAsync("REQ-036");

        result.DocumentationExists.ShouldBeTrue();
        result.Complete.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyDocumentationAsync_ParsesDocMissingResponse()
    {
        _mockCopilotService.SetResponse("""{"documentationExists": false, "issues": ["Missing tutorial"]}""");

        var result = await _service.VerifyDocumentationAsync("REQ-036");

        result.DocumentationExists.ShouldBeFalse();
        result.Issues.ShouldContain("Missing tutorial");
    }

    [Fact]
    public async Task VerifyBuildAsync_ParsesBuildSuccessResponse()
    {
        _mockCopilotService.SetResponse("""{"buildSucceeds": true, "issues": []}""");

        var result = await _service.VerifyBuildAsync();

        result.BuildSucceeds.ShouldBeTrue();
        result.Complete.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyBuildAsync_ParsesBuildFailureResponse()
    {
        _mockCopilotService.SetResponse("""{"buildSucceeds": false, "issues": ["Compile error"]}""");

        var result = await _service.VerifyBuildAsync();

        result.BuildSucceeds.ShouldBeFalse();
        result.Issues.ShouldContain("Compile error");
    }

    [Fact]
    public async Task VerifyRequirementCodeAsync_ValidCode_ReturnsComplete()
    {
        var result = await _service.VerifyRequirementCodeAsync("REQ-036");

        result.Complete.ShouldBeTrue();
        result.RequirementValid.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyRequirementCodeAsync_InvalidCode_ReturnsFailed()
    {
        var result = await _service.VerifyRequirementCodeAsync("REQ-INVALID");

        result.Complete.ShouldBeFalse();
        result.Issues.ShouldContain(i => i.Contains("REQ-INVALID"));
    }

    [Fact]
    public async Task VerifyRequirementCodeAsync_CaseInsensitive()
    {
        var result = await _service.VerifyRequirementCodeAsync("req-036");

        result.Complete.ShouldBeTrue();
        result.RequirementValid.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyRequirementCodeAsync_NoDocsDirectory_ReturnsFailed()
    {
        // Remove docs directory
        Directory.Delete(Path.Combine(_testDir, "docs"), recursive: true);

        var result = await _service.VerifyRequirementCodeAsync("REQ-036");

        result.Complete.ShouldBeFalse();
    }
}
