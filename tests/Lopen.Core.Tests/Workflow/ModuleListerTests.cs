using Lopen.Core.Workflow;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Core.Tests.Workflow;

public sealed class ModuleListerTests
{
    private const string ProjectRoot = "/project";
    private const string ReqDir = "/project/docs/requirements";

    private static (InMemoryFileSystem fs, ModuleLister lister) CreateLister(
        Action<InMemoryFileSystem>? setup = null)
    {
        var fs = new InMemoryFileSystem();
        fs.AddDirectory(ReqDir);
        setup?.Invoke(fs);

        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, ProjectRoot);
        var lister = new ModuleLister(scanner, fs, NullLogger<ModuleLister>.Instance);
        return (fs, lister);
    }

    [Fact]
    public void ListModules_EmptyProject_ReturnsEmpty()
    {
        var (_, lister) = CreateLister();

        var result = lister.ListModules();

        Assert.Empty(result);
    }

    [Fact]
    public void ListModules_ModuleWithNoSpec_ReturnsUnknown()
    {
        var (_, lister) = CreateLister(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
        });

        var result = lister.ListModules();

        Assert.Single(result);
        Assert.Equal("auth", result[0].Name);
        Assert.Equal(ModuleStatus.Unknown, result[0].Status);
        Assert.Equal(0, result[0].TotalCriteria);
    }

    [Fact]
    public void ListModules_ModuleWithNoCheckboxes_ReturnsNotStarted()
    {
        var (_, lister) = CreateLister(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
            fs.AddFile(ReqDir + "/auth/SPECIFICATION.md", "# Overview\n\nNo checkboxes here.");
        });

        var result = lister.ListModules();

        Assert.Single(result);
        Assert.Equal(ModuleStatus.NotStarted, result[0].Status);
        Assert.Equal(0, result[0].TotalCriteria);
    }

    [Fact]
    public void ListModules_AllCheckboxesUnchecked_ReturnsNotStarted()
    {
        var (_, lister) = CreateLister(fs =>
        {
            fs.AddDirectory(ReqDir + "/auth");
            fs.AddFile(ReqDir + "/auth/SPECIFICATION.md",
                "# AC\n\n- [ ] First\n- [ ] Second\n- [ ] Third");
        });

        var result = lister.ListModules();

        Assert.Equal(ModuleStatus.NotStarted, result[0].Status);
        Assert.Equal(0, result[0].CompletedCriteria);
        Assert.Equal(3, result[0].TotalCriteria);
    }

    [Fact]
    public void ListModules_SomeCheckboxesChecked_ReturnsInProgress()
    {
        var (_, lister) = CreateLister(fs =>
        {
            fs.AddDirectory(ReqDir + "/core");
            fs.AddFile(ReqDir + "/core/SPECIFICATION.md",
                "# AC\n\n- [x] Done\n- [ ] Pending\n- [x] Also done");
        });

        var result = lister.ListModules();

        Assert.Equal(ModuleStatus.InProgress, result[0].Status);
        Assert.Equal(2, result[0].CompletedCriteria);
        Assert.Equal(3, result[0].TotalCriteria);
    }

    [Fact]
    public void ListModules_AllCheckboxesChecked_ReturnsComplete()
    {
        var (_, lister) = CreateLister(fs =>
        {
            fs.AddDirectory(ReqDir + "/storage");
            fs.AddFile(ReqDir + "/storage/SPECIFICATION.md",
                "# AC\n\n- [x] First\n- [x] Second");
        });

        var result = lister.ListModules();

        Assert.Equal(ModuleStatus.Complete, result[0].Status);
        Assert.Equal(2, result[0].CompletedCriteria);
        Assert.Equal(2, result[0].TotalCriteria);
    }

    [Fact]
    public void ListModules_MultipleModules_SortedAlphabetically()
    {
        var (_, lister) = CreateLister(fs =>
        {
            fs.AddDirectory(ReqDir + "/zebra");
            fs.AddFile(ReqDir + "/zebra/SPECIFICATION.md", "# Z\n\n- [ ] todo");
            fs.AddDirectory(ReqDir + "/alpha");
            fs.AddFile(ReqDir + "/alpha/SPECIFICATION.md", "# A\n\n- [x] done");
            fs.AddDirectory(ReqDir + "/middle");
            fs.AddFile(ReqDir + "/middle/SPECIFICATION.md", "# M\n\n- [x] a\n- [ ] b");
        });

        var result = lister.ListModules();

        Assert.Equal(3, result.Count);
        Assert.Equal("alpha", result[0].Name);
        Assert.Equal("middle", result[1].Name);
        Assert.Equal("zebra", result[2].Name);
    }

    [Fact]
    public void ListModules_MultipleModules_CorrectStates()
    {
        var (_, lister) = CreateLister(fs =>
        {
            fs.AddDirectory(ReqDir + "/complete");
            fs.AddFile(ReqDir + "/complete/SPECIFICATION.md", "# AC\n\n- [x] done");
            fs.AddDirectory(ReqDir + "/inprogress");
            fs.AddFile(ReqDir + "/inprogress/SPECIFICATION.md", "# AC\n\n- [x] a\n- [ ] b");
            fs.AddDirectory(ReqDir + "/notstarted");
            fs.AddFile(ReqDir + "/notstarted/SPECIFICATION.md", "# AC\n\n- [ ] todo");
        });

        var result = lister.ListModules();

        Assert.Equal(ModuleStatus.Complete, result.First(m => m.Name == "complete").Status);
        Assert.Equal(ModuleStatus.InProgress, result.First(m => m.Name == "inprogress").Status);
        Assert.Equal(ModuleStatus.NotStarted, result.First(m => m.Name == "notstarted").Status);
    }

    [Fact]
    public void ListModules_SpecReadFails_ReturnsUnknown()
    {
        // Use a file system that doesn't have the file content
        var fs = new InMemoryFileSystem();
        fs.AddDirectory(ReqDir);
        fs.AddDirectory(ReqDir + "/broken");
        // Don't add the file, so ReadAllTextAsync will throw

        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, ProjectRoot);
        var lister = new ModuleLister(scanner, fs, NullLogger<ModuleLister>.Instance);

        var result = lister.ListModules();

        // ModuleScanner will report HasSpecification=false since file doesn't exist
        Assert.Single(result);
        Assert.Equal(ModuleStatus.Unknown, result[0].Status);
    }

    [Fact]
    public void Constructor_NullScanner_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ModuleLister(null!, new InMemoryFileSystem(), NullLogger<ModuleLister>.Instance));
    }

    [Fact]
    public void Constructor_NullFileSystem_Throws()
    {
        var scanner = new ModuleScanner(new InMemoryFileSystem(), NullLogger<ModuleScanner>.Instance, ProjectRoot);
        Assert.Throws<ArgumentNullException>(
            () => new ModuleLister(scanner, null!, NullLogger<ModuleLister>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var fs = new InMemoryFileSystem();
        var scanner = new ModuleScanner(fs, NullLogger<ModuleScanner>.Instance, ProjectRoot);
        Assert.Throws<ArgumentNullException>(
            () => new ModuleLister(scanner, fs, null!));
    }
}
