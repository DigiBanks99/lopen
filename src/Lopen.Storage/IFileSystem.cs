namespace Lopen.Storage;

/// <summary>
/// Abstraction over file system operations for testability.
/// </summary>
public interface IFileSystem
{
    /// <summary>Creates a directory (and all parents) if it does not exist.</summary>
    void CreateDirectory(string path);

    /// <summary>Returns true if the file exists.</summary>
    bool FileExists(string path);

    /// <summary>Returns true if the directory exists.</summary>
    bool DirectoryExists(string path);

    /// <summary>Reads the entire content of a file as text.</summary>
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Writes text to a file, creating or overwriting it.</summary>
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>Returns file paths in a directory matching an optional search pattern.</summary>
    IEnumerable<string> GetFiles(string path, string searchPattern = "*");

    /// <summary>Returns subdirectory paths in a directory.</summary>
    IEnumerable<string> GetDirectories(string path);

    /// <summary>Moves a file from source to destination.</summary>
    void MoveFile(string sourcePath, string destinationPath);

    /// <summary>Deletes a file if it exists.</summary>
    void DeleteFile(string path);

    /// <summary>Creates a symbolic link pointing to the target.</summary>
    void CreateSymlink(string linkPath, string targetPath);

    /// <summary>Resolves the target of a symbolic link.</summary>
    string? GetSymlinkTarget(string linkPath);

    /// <summary>Deletes a directory and all its contents.</summary>
    void DeleteDirectory(string path, bool recursive = true);

    /// <summary>Returns the last write time of a file in UTC.</summary>
    DateTime GetLastWriteTimeUtc(string path);
}
