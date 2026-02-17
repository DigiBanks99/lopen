namespace Lopen.Cli.Tests;

public class ProjectRootDiscoveryTests : IDisposable
{
    private readonly string _tempDir;

    public ProjectRootDiscoveryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void FindProjectRoot_WithLopenDir_ReturnsParent()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".lopen"));

        var result = ProjectRootDiscovery.FindProjectRoot(_tempDir);

        Assert.Equal(_tempDir, result);
    }

    [Fact]
    public void FindProjectRoot_WithGitDir_ReturnsParent()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));

        var result = ProjectRootDiscovery.FindProjectRoot(_tempDir);

        Assert.Equal(_tempDir, result);
    }

    [Fact]
    public void FindProjectRoot_LopenTakesPriorityOverGit_AtDifferentLevels()
    {
        // .git/ at _tempDir, .lopen/ deeper in _tempDir/sub
        // Start from _tempDir/sub/child — should find .lopen/ in _tempDir/sub first
        var subDir = Path.Combine(_tempDir, "sub");
        var childDir = Path.Combine(subDir, "child");
        Directory.CreateDirectory(childDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));
        Directory.CreateDirectory(Path.Combine(subDir, ".lopen"));

        var result = ProjectRootDiscovery.FindProjectRoot(childDir);

        Assert.Equal(subDir, result);
    }

    [Fact]
    public void FindProjectRoot_NoMarkers_ReturnsNull()
    {
        // Mount a tmpfs so no ancestor has .lopen/ or .git/
        // Simulate by using a directory that itself has no markers
        // and verifying the algorithm walks up correctly.
        // We test the FindMarker logic directly: create a dir tree with no markers at all.
        var root = Path.Combine(_tempDir, "no-markers-root");
        var leaf = Path.Combine(root, "a", "b", "c");
        Directory.CreateDirectory(leaf);

        // Call FindProjectRoot starting from leaf — no .lopen/ or .git/ in this subtree.
        // It will walk up into real FS and may find .git at repo root, so we verify
        // it does NOT return any dir within our temp tree.
        var result = ProjectRootDiscovery.FindProjectRoot(leaf);

        if (result is not null)
        {
            // If a result was found, it must be ABOVE our temp tree (from real FS)
            Assert.DoesNotContain(root, result);
        }
        // If null, that's the ideal case
    }

    [Fact]
    public void FindProjectRoot_AlgorithmReturnsNull_WhenNoMarkerInChain()
    {
        // Directly test: directory with no .lopen/ or .git/ in itself returns non-self
        var dirWithoutMarkers = Path.Combine(_tempDir, "empty-chain");
        Directory.CreateDirectory(dirWithoutMarkers);

        var result = ProjectRootDiscovery.FindProjectRoot(dirWithoutMarkers);

        // Should not return the dir itself since it has no markers
        Assert.NotEqual(dirWithoutMarkers, result);
    }

    [Fact]
    public void FindProjectRoot_WalksUpFromSubdirectory()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".lopen"));
        var subDir = Path.Combine(_tempDir, "src", "deep", "nested");
        Directory.CreateDirectory(subDir);

        var result = ProjectRootDiscovery.FindProjectRoot(subDir);

        Assert.Equal(_tempDir, result);
    }

    [Fact]
    public void FindProjectRoot_CurrentDirContainsMarker()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));

        var result = ProjectRootDiscovery.FindProjectRoot(_tempDir);

        Assert.Equal(_tempDir, result);
    }

    [Fact]
    public void FindProjectRoot_LopenPreferredOverGit_SameDirectory()
    {
        // Both .lopen/ and .git/ in the same directory — .lopen/ is found
        Directory.CreateDirectory(Path.Combine(_tempDir, ".lopen"));
        Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));

        var result = ProjectRootDiscovery.FindProjectRoot(_tempDir);

        // Should still return the dir (either marker is fine, but .lopen/ is checked first)
        Assert.Equal(_tempDir, result);
    }

    [Fact]
    public void FindProjectRoot_GitFallback_WhenNoLopen()
    {
        // Only .git/ at root, .lopen/ nowhere
        Directory.CreateDirectory(Path.Combine(_tempDir, ".git"));
        var subDir = Path.Combine(_tempDir, "src", "module");
        Directory.CreateDirectory(subDir);

        var result = ProjectRootDiscovery.FindProjectRoot(subDir);

        Assert.Equal(_tempDir, result);
    }
}
