using Lopen.Core.BackPressure;
using Lopen.Core.Documents;
using Lopen.Core.Git;
using Lopen.Core.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLopenCore_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddLopenCore();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddLopenCore_RegistersSpecificationParser()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<ISpecificationParser>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_RegistersContentHasher()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IContentHasher>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_RegistersGuardrailPipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IGuardrailPipeline>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_RegistersSingletons()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenCore();

        var provider = services.BuildServiceProvider();
        var parser1 = provider.GetService<ISpecificationParser>();
        var parser2 = provider.GetService<ISpecificationParser>();

        Assert.Same(parser1, parser2);
    }

    [Fact]
    public void AddLopenCore_WithProjectRoot_RegistersGitService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Lopen.Storage.IFileSystem, StubFileSystem>();
        services.AddLopenCore(projectRoot: "/tmp");

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IGitService>();

        Assert.NotNull(service);
    }

    [Fact]
    public void AddLopenCore_WithProjectRoot_RegistersModuleScanner()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<Lopen.Storage.IFileSystem, StubFileSystem>();
        services.AddLopenCore(projectRoot: "/tmp");

        var provider = services.BuildServiceProvider();
        var service = provider.GetService<IModuleScanner>();

        Assert.NotNull(service);
    }

    private sealed class StubFileSystem : Lopen.Storage.IFileSystem
    {
        public void CreateDirectory(string path) { }
        public bool FileExists(string path) => false;
        public bool DirectoryExists(string path) => false;
        public Task<string> ReadAllTextAsync(string path, CancellationToken ct = default) => Task.FromResult("");
        public Task WriteAllTextAsync(string path, string content, CancellationToken ct = default) => Task.CompletedTask;
        public IEnumerable<string> GetFiles(string path, string searchPattern = "*") => [];
        public IEnumerable<string> GetDirectories(string path) => [];
        public void MoveFile(string source, string dest) { }
        public void DeleteFile(string path) { }
        public void CreateSymlink(string linkPath, string targetPath) { }
        public string? GetSymlinkTarget(string linkPath) => null;
        public DateTime GetLastWriteTimeUtc(string path) => DateTime.UtcNow;
    }
}
