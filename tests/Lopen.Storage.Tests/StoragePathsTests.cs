namespace Lopen.Storage.Tests;

public class StoragePathsTests
{
    private const string ProjectRoot = "/home/user/myproject";

    [Fact]
    public void GetRoot_ReturnsLopenDirectory()
    {
        var result = StoragePaths.GetRoot(ProjectRoot);

        Assert.Equal(Path.Combine(ProjectRoot, ".lopen"), result);
    }

    [Fact]
    public void GetSessionsDirectory_ReturnsSessionsPath()
    {
        var result = StoragePaths.GetSessionsDirectory(ProjectRoot);

        Assert.Equal(Path.Combine(ProjectRoot, ".lopen", "sessions"), result);
    }

    [Fact]
    public void GetSessionDirectory_IncludesSessionId()
    {
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
        var result = StoragePaths.GetSessionDirectory(ProjectRoot, sessionId);

        Assert.Equal(Path.Combine(ProjectRoot, ".lopen", "sessions", "auth-20260214-1"), result);
    }

    [Fact]
    public void GetSessionStatePath_ReturnsStateJsonPath()
    {
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
        var result = StoragePaths.GetSessionStatePath(ProjectRoot, sessionId);

        Assert.EndsWith("state.json", result);
        Assert.Contains("auth-20260214-1", result);
    }

    [Fact]
    public void GetSessionMetricsPath_ReturnsMetricsJsonPath()
    {
        var sessionId = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
        var result = StoragePaths.GetSessionMetricsPath(ProjectRoot, sessionId);

        Assert.EndsWith("metrics.json", result);
        Assert.Contains("auth-20260214-1", result);
    }

    [Fact]
    public void GetLatestSymlinkPath_ReturnsLatestPath()
    {
        var result = StoragePaths.GetLatestSymlinkPath(ProjectRoot);

        Assert.EndsWith("latest", result);
        Assert.Contains("sessions", result);
    }

    [Fact]
    public void GetModulesDirectory_ReturnsModulesPath()
    {
        var result = StoragePaths.GetModulesDirectory(ProjectRoot);

        Assert.Equal(Path.Combine(ProjectRoot, ".lopen", "modules"), result);
    }

    [Fact]
    public void GetModuleDirectory_IncludesModuleName()
    {
        var result = StoragePaths.GetModuleDirectory(ProjectRoot, "auth");

        Assert.Equal(Path.Combine(ProjectRoot, ".lopen", "modules", "auth"), result);
    }

    [Fact]
    public void GetModulePlanPath_ReturnsPlanMdPath()
    {
        var result = StoragePaths.GetModulePlanPath(ProjectRoot, "auth");

        Assert.EndsWith("plan.md", result);
        Assert.Contains("auth", result);
    }

    [Fact]
    public void GetCacheDirectory_ReturnsCachePath()
    {
        var result = StoragePaths.GetCacheDirectory(ProjectRoot);

        Assert.Equal(Path.Combine(ProjectRoot, ".lopen", "cache"), result);
    }

    [Fact]
    public void GetSectionsCacheDirectory_ReturnsSectionsPath()
    {
        var result = StoragePaths.GetSectionsCacheDirectory(ProjectRoot);

        Assert.Equal(Path.Combine(ProjectRoot, ".lopen", "cache", "sections"), result);
    }

    [Fact]
    public void GetAssessmentsCacheDirectory_ReturnsAssessmentsPath()
    {
        var result = StoragePaths.GetAssessmentsCacheDirectory(ProjectRoot);

        Assert.Equal(Path.Combine(ProjectRoot, ".lopen", "cache", "assessments"), result);
    }

    [Fact]
    public void GetCorruptedDirectory_ReturnsCorruptedPath()
    {
        var result = StoragePaths.GetCorruptedDirectory(ProjectRoot);

        Assert.Equal(Path.Combine(ProjectRoot, ".lopen", "corrupted"), result);
    }

    [Fact]
    public void GetConfigPath_ReturnsConfigJsonPath()
    {
        var result = StoragePaths.GetConfigPath(ProjectRoot);

        Assert.Equal(Path.Combine(ProjectRoot, ".lopen", "config.json"), result);
    }

    // ==================== STOR-20: Research document paths ====================

    [Fact]
    public void GetRequirementsDirectory_ReturnsDocsRequirementsPath()
    {
        var result = StoragePaths.GetRequirementsDirectory(ProjectRoot);

        Assert.Equal(Path.Combine(ProjectRoot, "docs", "requirements"), result);
    }

    [Fact]
    public void GetModuleRequirementsDirectory_IncludesModuleName()
    {
        var result = StoragePaths.GetModuleRequirementsDirectory(ProjectRoot, "auth");

        Assert.Equal(Path.Combine(ProjectRoot, "docs", "requirements", "auth"), result);
    }

    [Fact]
    public void GetResearchDocumentPath_ReturnsCorrectPath()
    {
        var result = StoragePaths.GetResearchDocumentPath(ProjectRoot, "auth", "oauth-flow");

        Assert.Equal(Path.Combine(ProjectRoot, "docs", "requirements", "auth", "RESEARCH-oauth-flow.md"), result);
    }

    [Fact]
    public void GetResearchIndexPath_ReturnsResearchMdPath()
    {
        var result = StoragePaths.GetResearchIndexPath(ProjectRoot, "auth");

        Assert.Equal(Path.Combine(ProjectRoot, "docs", "requirements", "auth", "RESEARCH.md"), result);
    }

    [Fact]
    public void GetResearchDocumentPath_InSourceNotLopen()
    {
        var result = StoragePaths.GetResearchDocumentPath(ProjectRoot, "core", "state-machine");

        Assert.DoesNotContain(".lopen", result);
        Assert.Contains("docs", result);
    }
}
