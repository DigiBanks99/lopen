using Lopen.Core.Documents;
using Lopen.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Documents;

public sealed class ResourceTrackerTests
{
    private const string ProjectRoot = "/proj";

    private readonly InMemoryFileSystem _fs = new();

    private ResourceTracker CreateTracker() =>
        new(_fs, ProjectRoot, NullLogger<ResourceTracker>.Instance);

    [Fact]
    public async Task GetActiveResourcesAsync_NullModuleName_ThrowsArgumentException()
    {
        var tracker = CreateTracker();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => tracker.GetActiveResourcesAsync(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetActiveResourcesAsync_EmptyModuleName_ThrowsArgumentException(string moduleName)
    {
        var tracker = CreateTracker();
        await Assert.ThrowsAsync<ArgumentException>(
            () => tracker.GetActiveResourcesAsync(moduleName));
    }

    [Fact]
    public async Task GetActiveResourcesAsync_NoRequirementsDirectory_ReturnsEmpty()
    {
        var tracker = CreateTracker();
        var result = await tracker.GetActiveResourcesAsync("auth");
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetActiveResourcesAsync_WithSpecification_ReturnsSpecResource()
    {
        _fs.AddDirectory("/proj/docs/requirements/auth");
        _fs.AddFile("/proj/docs/requirements/auth/SPECIFICATION.md", "# Spec content");

        var tracker = CreateTracker();
        var result = await tracker.GetActiveResourcesAsync("auth");

        Assert.Contains(result, r => r.Label == "SPECIFICATION.md");
        // All results should be from the spec file (InMemoryFileSystem.GetFiles ignores pattern)
        Assert.All(result, r => Assert.Equal("SPECIFICATION.md", r.Label));
    }

    [Fact]
    public async Task GetActiveResourcesAsync_WithResearchIndex_ReturnsResearchResource()
    {
        _fs.AddDirectory("/proj/docs/requirements/auth");
        _fs.AddFile("/proj/docs/requirements/auth/RESEARCH.md", "# Research index");

        var tracker = CreateTracker();
        var result = await tracker.GetActiveResourcesAsync("auth");

        Assert.Contains(result, r => r.Label == "RESEARCH.md");
        Assert.All(result, r => Assert.Equal("RESEARCH.md", r.Label));
    }

    [Fact]
    public async Task GetActiveResourcesAsync_WithResearchTopicFiles_ReturnsAll()
    {
        _fs.AddDirectory("/proj/docs/requirements/auth");
        _fs.AddFile("/proj/docs/requirements/auth/RESEARCH-jwt.md", "# JWT research");
        _fs.AddFile("/proj/docs/requirements/auth/RESEARCH-oauth.md", "# OAuth research");

        var tracker = CreateTracker();
        var result = await tracker.GetActiveResourcesAsync("auth");

        Assert.Contains(result, r => r.Label == "RESEARCH-jwt.md");
        Assert.Contains(result, r => r.Label == "RESEARCH-oauth.md");
    }

    [Fact]
    public async Task GetActiveResourcesAsync_WithPlanFile_ReturnsPlanResource()
    {
        _fs.AddFile("/proj/.lopen/modules/auth/plan.md", "# Plan content");

        var tracker = CreateTracker();
        var result = await tracker.GetActiveResourcesAsync("auth");

        Assert.Single(result);
        Assert.Equal("plan.md", result[0].Label);
    }

    [Fact]
    public async Task GetActiveResourcesAsync_WithAllFiles_ReturnsAllResources()
    {
        _fs.AddDirectory("/proj/docs/requirements/auth");
        _fs.AddFile("/proj/docs/requirements/auth/SPECIFICATION.md", "# Spec");
        _fs.AddFile("/proj/docs/requirements/auth/RESEARCH.md", "# Research");
        _fs.AddFile("/proj/docs/requirements/auth/RESEARCH-jwt.md", "# JWT");
        _fs.AddFile("/proj/.lopen/modules/auth/plan.md", "# Plan");

        var tracker = CreateTracker();
        var result = await tracker.GetActiveResourcesAsync("auth");

        Assert.Contains(result, r => r.Label == "SPECIFICATION.md");
        Assert.Contains(result, r => r.Label == "RESEARCH.md");
        Assert.Contains(result, r => r.Label == "RESEARCH-jwt.md");
        Assert.Contains(result, r => r.Label == "plan.md");
        Assert.True(result.Count >= 4);
    }

    [Fact]
    public async Task GetActiveResourcesAsync_ReadFailure_SkipsResource()
    {
        var inner = new InMemoryFileSystem();
        inner.AddDirectory("/proj/docs/requirements/auth");
        inner.AddFile("/proj/docs/requirements/auth/SPECIFICATION.md", "will fail");
        inner.AddFile("/proj/docs/requirements/auth/RESEARCH.md", "# Research");

        var failFs = new FailingReadFileSystem(inner, "/proj/docs/requirements/auth/SPECIFICATION.md");
        var tracker = new ResourceTracker(failFs, ProjectRoot, NullLogger<ResourceTracker>.Instance);
        var result = await tracker.GetActiveResourcesAsync("auth");

        // SPECIFICATION.md should be skipped, only RESEARCH.md should appear
        Assert.DoesNotContain(result, r => r.Label == "SPECIFICATION.md");
        Assert.Contains(result, r => r.Label == "RESEARCH.md");
    }

    [Fact]
    public async Task GetActiveResourcesAsync_ResourcesContainContent()
    {
        _fs.AddDirectory("/proj/docs/requirements/auth");
        _fs.AddFile("/proj/docs/requirements/auth/SPECIFICATION.md", "spec body here");
        _fs.AddFile("/proj/.lopen/modules/auth/plan.md", "plan body here");

        var tracker = CreateTracker();
        var result = await tracker.GetActiveResourcesAsync("auth");

        var spec = result.First(r => r.Label == "SPECIFICATION.md");
        Assert.Equal("spec body here", spec.Content);

        var plan = result.First(r => r.Label == "plan.md");
        Assert.Equal("plan body here", plan.Content);
    }

    /// <summary>
    /// Decorator over InMemoryFileSystem that throws on ReadAllTextAsync for a specific path.
    /// </summary>
    private sealed class FailingReadFileSystem(InMemoryFileSystem inner, string failPath) : IFileSystem
    {
        public void CreateDirectory(string path) => inner.CreateDirectory(path);
        public bool FileExists(string path) => inner.FileExists(path);
        public bool DirectoryExists(string path) => inner.DirectoryExists(path);
        public Task WriteAllTextAsync(string path, string content, CancellationToken ct = default) => inner.WriteAllTextAsync(path, content, ct);
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*") => inner.GetFiles(path, searchPattern);
        public IEnumerable<string> GetDirectories(string path) => inner.GetDirectories(path);
        public void MoveFile(string s, string d) => inner.MoveFile(s, d);
        public void DeleteFile(string path) => inner.DeleteFile(path);
        public void CreateSymlink(string l, string t) => inner.CreateSymlink(l, t);
        public string? GetSymlinkTarget(string path) => inner.GetSymlinkTarget(path);
        public void DeleteDirectory(string path, bool recursive = true) => inner.DeleteDirectory(path, recursive);
        public DateTime GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            var normalized = path.Replace('\\', '/').TrimEnd('/');
            var failNormalized = failPath.Replace('\\', '/').TrimEnd('/');
            if (string.Equals(normalized, failNormalized, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Simulated read failure");
            return inner.ReadAllTextAsync(path, cancellationToken);
        }
    }
}
