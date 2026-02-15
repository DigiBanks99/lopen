using Lopen.Core.Workflow;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Workflow;

public class ModuleScannerTests
{
    [Fact]
    public void ScanModules_NoRequirementsDirectory_ReturnsEmpty()
    {
        var fs = new TestFileSystem();
        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, "/project");

        var modules = scanner.ScanModules();

        Assert.Empty(modules);
    }

    [Fact]
    public void ScanModules_EmptyRequirementsDirectory_ReturnsEmpty()
    {
        var fs = new TestFileSystem();
        fs.AddDirectory("/project/docs/requirements");
        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, "/project");

        var modules = scanner.ScanModules();

        Assert.Empty(modules);
    }

    [Fact]
    public void ScanModules_ModuleWithSpec_ReturnsWithHasSpecTrue()
    {
        var fs = new TestFileSystem();
        fs.AddDirectory("/project/docs/requirements");
        fs.AddDirectory("/project/docs/requirements/auth");
        fs.AddFile("/project/docs/requirements/auth/SPECIFICATION.md");
        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, "/project");

        var modules = scanner.ScanModules();

        Assert.Single(modules);
        Assert.Equal("auth", modules[0].Name);
        Assert.True(modules[0].HasSpecification);
    }

    [Fact]
    public void ScanModules_ModuleWithoutSpec_ReturnsWithHasSpecFalse()
    {
        var fs = new TestFileSystem();
        fs.AddDirectory("/project/docs/requirements");
        fs.AddDirectory("/project/docs/requirements/auth");
        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, "/project");

        var modules = scanner.ScanModules();

        Assert.Single(modules);
        Assert.Equal("auth", modules[0].Name);
        Assert.False(modules[0].HasSpecification);
    }

    [Fact]
    public void ScanModules_MultipleModules_ReturnsAll()
    {
        var fs = new TestFileSystem();
        fs.AddDirectory("/project/docs/requirements");
        fs.AddDirectory("/project/docs/requirements/auth");
        fs.AddFile("/project/docs/requirements/auth/SPECIFICATION.md");
        fs.AddDirectory("/project/docs/requirements/storage");
        fs.AddFile("/project/docs/requirements/storage/SPECIFICATION.md");
        fs.AddDirectory("/project/docs/requirements/llm");
        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, "/project");

        var modules = scanner.ScanModules();

        Assert.Equal(3, modules.Count);
        Assert.Contains(modules, m => m.Name == "auth" && m.HasSpecification);
        Assert.Contains(modules, m => m.Name == "storage" && m.HasSpecification);
        Assert.Contains(modules, m => m.Name == "llm" && !m.HasSpecification);
    }

    [Fact]
    public void ScanModules_SpecificationPath_IncludesFullPath()
    {
        var fs = new TestFileSystem();
        fs.AddDirectory("/project/docs/requirements");
        fs.AddDirectory("/project/docs/requirements/core");
        fs.AddFile("/project/docs/requirements/core/SPECIFICATION.md");
        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, "/project");

        var modules = scanner.ScanModules();

        Assert.Contains("SPECIFICATION.md", modules[0].SpecificationPath);
        Assert.Contains("core", modules[0].SpecificationPath);
    }

    [Fact]
    public void Constructor_NullFileSystem_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ModuleScanner(null!, NullLogger<ModuleScanner>.Instance, "/project"));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ModuleScanner(new TestFileSystem(), null!, "/project"));
    }

    [Fact]
    public void Constructor_NullProjectRoot_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ModuleScanner(new TestFileSystem(), NullLogger<ModuleScanner>.Instance, null!));
    }

    /// <summary>Simple in-memory file system for testing ModuleScanner.</summary>
    private sealed class TestFileSystem : Lopen.Storage.IFileSystem
    {
        private readonly HashSet<string> _directories = [];
        private readonly HashSet<string> _files = [];

        public void AddDirectory(string path) => _directories.Add(path);
        public void AddFile(string path) => _files.Add(path);

        public bool DirectoryExists(string path) => _directories.Contains(path);
        public bool FileExists(string path) => _files.Contains(path);

        public IEnumerable<string> GetDirectories(string path) =>
            _directories.Where(d => Path.GetDirectoryName(d) == path);

        public void CreateDirectory(string path) => _directories.Add(path);
        public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) => Task.FromResult("");
        public Task WriteAllTextAsync(string path, string content, CancellationToken ct = default) => Task.CompletedTask;
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*") => [];
        public void MoveFile(string source, string dest) { }
        public void DeleteFile(string path) { }
        public void CreateSymlink(string linkPath, string targetPath) { }
        public string? GetSymlinkTarget(string linkPath) => null;
        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;
    }
}
