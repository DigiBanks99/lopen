namespace Lopen.Storage;

/// <summary>
/// Exception thrown when a storage operation fails (disk errors, corrupted state).
/// </summary>
public class StorageException : Exception
{
    /// <summary>The file path involved in the failure, if applicable.</summary>
    public string? Path { get; }

    /// <summary>
    /// Whether this represents a critical write failure (disk full, permission denied)
    /// that should block the workflow (STOR-16).
    /// </summary>
    public bool IsCritical => InnerException is IOException or UnauthorizedAccessException;

    public StorageException(string message)
        : base(message) { }

    public StorageException(string message, string? path)
        : base(message)
    {
        Path = path;
    }

    public StorageException(string message, string? path, Exception innerException)
        : base(message, innerException)
    {
        Path = path;
    }
}

/// <summary>
/// Thrown when a write fails due to disk full or similar I/O condition (STOR-16).
/// Contains OS error details for user-facing diagnostics.
/// </summary>
public class WriteFailureStorageException : StorageException
{
    /// <summary>OS-level error code (HResult).</summary>
    public int OsErrorCode { get; }

    /// <summary>Human-readable description of the OS error.</summary>
    public string OsErrorDescription { get; }

    public WriteFailureStorageException(string message, string? path, IOException innerException)
        : base(message, path, innerException)
    {
        OsErrorCode = innerException.HResult;
        OsErrorDescription = ClassifyWriteError(innerException);
    }

    private static string ClassifyWriteError(IOException ex)
    {
        // Windows: ERROR_DISK_FULL / ERROR_HANDLE_DISK_FULL
        const int ERROR_DISK_FULL = unchecked((int)0x80070070);
        const int ERROR_HANDLE_DISK_FULL = unchecked((int)0x80070027);
        // Linux/macOS: ENOSPC (28)
        const int LINUX_ENOSPC = unchecked((int)0x8007001C);

        return ex.HResult switch
        {
            ERROR_DISK_FULL or ERROR_HANDLE_DISK_FULL => "Disk full",
            LINUX_ENOSPC => "No space left on device (ENOSPC)",
            _ => $"I/O write failure (HResult: 0x{ex.HResult:X8})"
        };
    }
}
