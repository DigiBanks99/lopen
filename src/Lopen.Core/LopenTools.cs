using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Lopen.Core;

/// <summary>
/// Built-in tools for Lopen AI sessions.
/// </summary>
public static class LopenTools
{
    /// <summary>
    /// Tool to read file contents.
    /// </summary>
    public static AIFunction ReadFile() =>
        AIFunctionFactory.Create(ReadFileImpl, "lopen_read_file", "Read the contents of a file");

    /// <summary>
    /// Tool to list directory contents.
    /// </summary>
    public static AIFunction ListDirectory() =>
        AIFunctionFactory.Create(ListDirectoryImpl, "lopen_list_directory", "List files and directories in a path");

    /// <summary>
    /// Tool to get current working directory.
    /// </summary>
    public static AIFunction GetWorkingDirectory() =>
        AIFunctionFactory.Create(GetWorkingDirectoryImpl, "lopen_get_cwd", "Get the current working directory");

    /// <summary>
    /// Tool to check if a file exists.
    /// </summary>
    public static AIFunction FileExists() =>
        AIFunctionFactory.Create(FileExistsImpl, "lopen_file_exists", "Check if a file or directory exists");

    /// <summary>
    /// Gets all built-in Lopen tools.
    /// </summary>
    public static ICollection<AIFunction> GetAll() =>
    [
        ReadFile(),
        ListDirectory(),
        GetWorkingDirectory(),
        FileExists()
    ];

    // Tool implementations

    private static string ReadFileImpl([Description("Path to the file to read")] string path)
    {
        if (string.IsNullOrEmpty(path))
            return "Error: Path cannot be empty";

        try
        {
            if (!File.Exists(path))
                return $"Error: File not found: {path}";

            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"Error reading file: {ex.Message}";
        }
    }

    private static string ListDirectoryImpl([Description("Path to the directory to list")] string path)
    {
        if (string.IsNullOrEmpty(path))
            path = Directory.GetCurrentDirectory();

        try
        {
            if (!Directory.Exists(path))
                return $"Error: Directory not found: {path}";

            var entries = Directory.GetFileSystemEntries(path)
                .Select(e => Path.GetFileName(e))
                .OrderBy(e => e);

            return string.Join("\n", entries);
        }
        catch (Exception ex)
        {
            return $"Error listing directory: {ex.Message}";
        }
    }

    private static string GetWorkingDirectoryImpl()
    {
        return Directory.GetCurrentDirectory();
    }

    private static bool FileExistsImpl([Description("Path to check")] string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return File.Exists(path) || Directory.Exists(path);
    }
}
