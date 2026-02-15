namespace Lopen.Storage.Tests;

public class PhysicalFileSystemTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PhysicalFileSystem _fileSystem;

    public PhysicalFileSystemTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "lopen-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _fileSystem = new PhysicalFileSystem();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void CreateDirectory_CreatesNestedDirectories()
    {
        var path = Path.Combine(_tempDir, "a", "b", "c");

        _fileSystem.CreateDirectory(path);

        Assert.True(Directory.Exists(path));
    }

    [Fact]
    public void FileExists_ReturnsFalse_WhenFileDoesNotExist()
    {
        Assert.False(_fileSystem.FileExists(Path.Combine(_tempDir, "nonexistent.txt")));
    }

    [Fact]
    public void FileExists_ReturnsTrue_WhenFileExists()
    {
        var path = Path.Combine(_tempDir, "exists.txt");
        File.WriteAllText(path, "content");

        Assert.True(_fileSystem.FileExists(path));
    }

    [Fact]
    public void DirectoryExists_ReturnsFalse_WhenDirectoryDoesNotExist()
    {
        Assert.False(_fileSystem.DirectoryExists(Path.Combine(_tempDir, "nonexistent")));
    }

    [Fact]
    public void DirectoryExists_ReturnsTrue_WhenDirectoryExists()
    {
        Assert.True(_fileSystem.DirectoryExists(_tempDir));
    }

    [Fact]
    public async Task WriteAllTextAsync_And_ReadAllTextAsync_RoundTrips()
    {
        var path = Path.Combine(_tempDir, "test.txt");

        await _fileSystem.WriteAllTextAsync(path, "hello world");
        var content = await _fileSystem.ReadAllTextAsync(path);

        Assert.Equal("hello world", content);
    }

    [Fact]
    public void GetFiles_ReturnsFilesInDirectory()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "");
        File.WriteAllText(Path.Combine(_tempDir, "b.txt"), "");

        var files = _fileSystem.GetFiles(_tempDir).ToList();

        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void GetDirectories_ReturnsSubdirectories()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub1"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub2"));

        var dirs = _fileSystem.GetDirectories(_tempDir).ToList();

        Assert.Equal(2, dirs.Count);
    }

    [Fact]
    public void MoveFile_MovesFileToDestination()
    {
        var src = Path.Combine(_tempDir, "src.txt");
        var dst = Path.Combine(_tempDir, "dst.txt");
        File.WriteAllText(src, "content");

        _fileSystem.MoveFile(src, dst);

        Assert.False(File.Exists(src));
        Assert.True(File.Exists(dst));
        Assert.Equal("content", File.ReadAllText(dst));
    }

    [Fact]
    public void DeleteFile_RemovesFile()
    {
        var path = Path.Combine(_tempDir, "delete-me.txt");
        File.WriteAllText(path, "content");

        _fileSystem.DeleteFile(path);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public void GetLastWriteTimeUtc_ReturnsReasonableTime()
    {
        var path = Path.Combine(_tempDir, "timed.txt");
        File.WriteAllText(path, "content");

        var time = _fileSystem.GetLastWriteTimeUtc(path);

        Assert.True(time > DateTime.MinValue);
        Assert.True(time <= DateTime.UtcNow);
    }

    [Fact]
    public void CreateSymlink_And_GetSymlinkTarget_RoundTrips()
    {
        var targetDir = Path.Combine(_tempDir, "target");
        Directory.CreateDirectory(targetDir);
        var linkPath = Path.Combine(_tempDir, "link");

        _fileSystem.CreateSymlink(linkPath, targetDir);
        var target = _fileSystem.GetSymlinkTarget(linkPath);

        Assert.Equal(targetDir, target);
    }
}
