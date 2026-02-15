namespace Lopen.Storage;

/// <summary>
/// Exception thrown when a storage operation fails (disk errors, corrupted state).
/// </summary>
public class StorageException : Exception
{
    /// <summary>The file path involved in the failure, if applicable.</summary>
    public string? Path { get; }

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
