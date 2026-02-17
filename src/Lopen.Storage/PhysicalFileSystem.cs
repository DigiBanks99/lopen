namespace Lopen.Storage;

/// <summary>
/// Production file system implementation wrapping <see cref="System.IO"/>.
/// </summary>
internal sealed class PhysicalFileSystem : IFileSystem
{
    public void CreateDirectory(string path) =>
        Directory.CreateDirectory(path);

    public bool FileExists(string path) =>
        File.Exists(path);

    public bool DirectoryExists(string path) =>
        Directory.Exists(path);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(path, cancellationToken);

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(path, content, cancellationToken);

    public IEnumerable<string> GetFiles(string path, string searchPattern = "*") =>
        Directory.GetFiles(path, searchPattern);

    public IEnumerable<string> GetDirectories(string path) =>
        Directory.GetDirectories(path);

    public void MoveFile(string sourcePath, string destinationPath) =>
        File.Move(sourcePath, destinationPath, overwrite: true);

    public void DeleteFile(string path) =>
        File.Delete(path);

    public void CreateSymlink(string linkPath, string targetPath)
    {
        if (File.Exists(linkPath) || Directory.Exists(linkPath))
        {
            // Remove existing symlink before creating a new one
            if (File.GetAttributes(linkPath).HasFlag(FileAttributes.ReparsePoint))
            {
                Directory.Delete(linkPath);
            }
        }

        Directory.CreateSymbolicLink(linkPath, targetPath);
    }

    public string? GetSymlinkTarget(string linkPath)
    {
        var info = new FileInfo(linkPath);
        return info.LinkTarget;
    }

    public void DeleteDirectory(string path, bool recursive = true) =>
        Directory.Delete(path, recursive);

    public DateTime GetLastWriteTimeUtc(string path) =>
        File.GetLastWriteTimeUtc(path);
}
