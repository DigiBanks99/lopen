using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Storage.Tests;

public class StorageInitializerTests
{
    [Fact]
    public void EnsureDirectoryStructure_CreatesAllDirectories()
    {
        var fs = new InMemoryFileSystem();
        var initializer = new StorageInitializer(fs, NullLogger<StorageInitializer>.Instance, "/project");

        initializer.EnsureDirectoryStructure();

        Assert.True(fs.DirectoryExists("/project/.lopen"));
        Assert.True(fs.DirectoryExists("/project/.lopen/sessions"));
        Assert.True(fs.DirectoryExists("/project/.lopen/modules"));
        Assert.True(fs.DirectoryExists("/project/.lopen/cache"));
        Assert.True(fs.DirectoryExists("/project/.lopen/cache/sections"));
        Assert.True(fs.DirectoryExists("/project/.lopen/cache/assessments"));
        Assert.True(fs.DirectoryExists("/project/.lopen/corrupted"));
    }

    [Fact]
    public void EnsureDirectoryStructure_Idempotent_NoError()
    {
        var fs = new InMemoryFileSystem();
        var initializer = new StorageInitializer(fs, NullLogger<StorageInitializer>.Instance, "/project");

        initializer.EnsureDirectoryStructure();
        initializer.EnsureDirectoryStructure(); // Second call should not throw

        Assert.True(fs.DirectoryExists("/project/.lopen"));
    }

    [Fact]
    public void EnsureDirectoryStructure_ExistingDirs_NoError()
    {
        var fs = new InMemoryFileSystem();
        fs.CreateDirectory("/project/.lopen");
        fs.CreateDirectory("/project/.lopen/sessions");

        var initializer = new StorageInitializer(fs, NullLogger<StorageInitializer>.Instance, "/project");
        initializer.EnsureDirectoryStructure();

        Assert.True(fs.DirectoryExists("/project/.lopen/modules"));
    }

    [Fact]
    public void Constructor_NullFileSystem_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StorageInitializer(null!, NullLogger<StorageInitializer>.Instance, "/project"));
    }

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StorageInitializer(new InMemoryFileSystem(), null!, "/project"));
    }

    [Fact]
    public void Constructor_EmptyProjectRoot_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new StorageInitializer(new InMemoryFileSystem(), NullLogger<StorageInitializer>.Instance, ""));
    }
}
