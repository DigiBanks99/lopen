using Lopen.Core.Documents;
using Lopen.Core.Workflow;
using Lopen.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Documents;

public class SpecificationDriftServiceTests
{
    private readonly StubDriftDetector _driftDetector = new();
    private readonly StubSpecificationParser _parser = new();
    private readonly StubContentHasher _hasher = new();
    private readonly StubModuleScanner _moduleScanner = new();
    private readonly StubFileSystem _fileSystem = new();

    private SpecificationDriftService CreateService() => new(
        _driftDetector, _parser, _hasher, _moduleScanner, _fileSystem,
        NullLogger<SpecificationDriftService>.Instance);

    [Fact]
    public async Task CheckDriftAsync_ReturnsEmpty_WhenModuleNotFound()
    {
        _moduleScanner.Modules = [];
        var sut = CreateService();

        var result = await sut.CheckDriftAsync("unknown");

        Assert.Empty(result);
    }

    [Fact]
    public async Task CheckDriftAsync_ReturnsEmpty_WhenNoSpecification()
    {
        _moduleScanner.Modules = [new ModuleInfo("test", "/specs/test/SPEC.md", false)];
        var sut = CreateService();

        var result = await sut.CheckDriftAsync("test");

        Assert.Empty(result);
    }

    [Fact]
    public async Task CheckDriftAsync_ReturnsEmpty_WhenFileDoesNotExist()
    {
        _moduleScanner.Modules = [new ModuleInfo("test", "/specs/test/SPEC.md", true)];
        _fileSystem.ExistingFiles.Clear();
        var sut = CreateService();

        var result = await sut.CheckDriftAsync("test");

        Assert.Empty(result);
    }

    [Fact]
    public async Task CheckDriftAsync_CallsDriftDetector_WithCurrentContent()
    {
        _moduleScanner.Modules = [new ModuleInfo("test", "/specs/test/SPEC.md", true)];
        _fileSystem.ExistingFiles["/specs/test/SPEC.md"] = "# Section\nContent here";
        _driftDetector.Results = [];
        var sut = CreateService();

        await sut.CheckDriftAsync("test");

        Assert.Equal("/specs/test/SPEC.md", _driftDetector.LastSpecPath);
        Assert.Equal("# Section\nContent here", _driftDetector.LastContent);
    }

    [Fact]
    public async Task CheckDriftAsync_ReturnsDriftResults_WhenDriftDetected()
    {
        _moduleScanner.Modules = [new ModuleInfo("test", "/specs/test/SPEC.md", true)];
        _fileSystem.ExistingFiles["/specs/test/SPEC.md"] = "# Changed\nNew content";
        var drift = new DriftResult("Changed", "abc", "xyz", false, false);
        _driftDetector.Results = [drift];
        var sut = CreateService();

        var result = await sut.CheckDriftAsync("test");

        Assert.Single(result);
        Assert.Equal("Changed", result[0].Header);
    }

    [Fact]
    public async Task CheckDriftAsync_FirstCall_ReturnsNoDrift_ThenSecondCall_DetectsDrift()
    {
        _moduleScanner.Modules = [new ModuleInfo("test", "/specs/test/SPEC.md", true)];
        _fileSystem.ExistingFiles["/specs/test/SPEC.md"] = "# Section\nOriginal";
        _parser.Sections = [new DocumentSection("Section", 1, "Original")];
        _hasher.Hash = "hash1";
        _driftDetector.Results = [];
        var sut = CreateService();

        // First call — no cached sections, drift detector gets empty list
        var firstResult = await sut.CheckDriftAsync("test");
        Assert.Empty(firstResult);

        // Verify cached sections were populated (will be passed on next call)
        Assert.NotNull(_driftDetector.LastCachedSections);
        Assert.Empty(_driftDetector.LastCachedSections);

        // Second call — now cached sections exist
        _fileSystem.ExistingFiles["/specs/test/SPEC.md"] = "# Section\nModified";
        _driftDetector.Results = [new DriftResult("Section", "hash1", "hash2", false, false)];

        var secondResult = await sut.CheckDriftAsync("test");
        Assert.Single(secondResult);
        Assert.Single(_driftDetector.LastCachedSections!);
        Assert.Equal("hash1", _driftDetector.LastCachedSections![0].ContentHash);
    }

    [Fact]
    public async Task CheckDriftAsync_ThrowsOnNullModuleName()
    {
        var sut = CreateService();
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.CheckDriftAsync(null!));
    }

    [Fact]
    public async Task CheckDriftAsync_ThrowsOnEmptyModuleName()
    {
        var sut = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() => sut.CheckDriftAsync(""));
    }

    [Fact]
    public async Task CheckDriftAsync_CaseInsensitiveModuleLookup()
    {
        _moduleScanner.Modules = [new ModuleInfo("Core", "/specs/core/SPEC.md", true)];
        _fileSystem.ExistingFiles["/specs/core/SPEC.md"] = "# Section\nContent";
        _driftDetector.Results = [];
        var sut = CreateService();

        var result = await sut.CheckDriftAsync("core");

        Assert.NotNull(_driftDetector.LastSpecPath);
    }

    // --- Stubs ---

    private sealed class StubDriftDetector : IDriftDetector
    {
        public IReadOnlyList<DriftResult> Results { get; set; } = [];
        public string? LastSpecPath { get; private set; }
        public string? LastContent { get; private set; }
        public IReadOnlyList<CachedSection>? LastCachedSections { get; private set; }

        public IReadOnlyList<DriftResult> DetectDrift(
            string specificationPath, string currentContent,
            IReadOnlyList<CachedSection> cachedSections)
        {
            LastSpecPath = specificationPath;
            LastContent = currentContent;
            LastCachedSections = cachedSections;
            return Results;
        }
    }

    private sealed class StubSpecificationParser : ISpecificationParser
    {
        public IReadOnlyList<DocumentSection> Sections { get; set; } =
            [new DocumentSection("Section", 1, "Content")];

        public IReadOnlyList<DocumentSection> ExtractSections(string content) => Sections;
        public string? ExtractSection(string content, string header) =>
            Sections.FirstOrDefault(s => s.Header == header)?.Content;
    }

    private sealed class StubContentHasher : IContentHasher
    {
        public string Hash { get; set; } = "testhash";
        public string ComputeHash(string content) => Hash;
        public bool HasDrifted(string content, string expectedHash) => ComputeHash(content) != expectedHash;
    }

    private sealed class StubModuleScanner : IModuleScanner
    {
        public IReadOnlyList<ModuleInfo> Modules { get; set; } = [];
        public IReadOnlyList<ModuleInfo> ScanModules() => Modules;
    }

    private sealed class StubFileSystem : IFileSystem
    {
        public Dictionary<string, string> ExistingFiles { get; } = new();

        public bool FileExists(string path) => ExistingFiles.ContainsKey(path);
        public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) =>
            Task.FromResult(ExistingFiles.TryGetValue(path, out var content) ? content : "");
        public Task WriteAllTextAsync(string path, string content, CancellationToken ct = default)
        {
            ExistingFiles[path] = content;
            return Task.CompletedTask;
        }
        public bool DirectoryExists(string path) => true;
        public void CreateDirectory(string path) { }
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*") => [];
        public IEnumerable<string> GetDirectories(string path) => [];
        public void MoveFile(string sourcePath, string destinationPath) { }
        public void DeleteFile(string path) { }
        public void CreateSymlink(string linkPath, string targetPath) { }
        public string? GetSymlinkTarget(string linkPath) => null;
        public void DeleteDirectory(string path, bool recursive = true) { }
        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;
    }
}
