namespace Lopen.Core;

/// <summary>
/// Standard exit codes for CLI commands.
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// Command completed successfully.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// General unspecified error.
    /// </summary>
    public const int GeneralError = 1;

    /// <summary>
    /// Invalid command line arguments.
    /// </summary>
    public const int InvalidArguments = 2;

    /// <summary>
    /// Authentication error (not logged in, token expired, etc.).
    /// </summary>
    public const int AuthenticationError = 3;

    /// <summary>
    /// Network error (timeout, connection refused, etc.).
    /// </summary>
    public const int NetworkError = 4;

    /// <summary>
    /// Copilot SDK error.
    /// </summary>
    public const int CopilotError = 5;

    /// <summary>
    /// Configuration error.
    /// </summary>
    public const int ConfigurationError = 6;

    /// <summary>
    /// User cancelled the operation.
    /// </summary>
    public const int Cancelled = 130;

    /// <summary>
    /// Gets a human-readable description for an exit code.
    /// </summary>
    public static string GetDescription(int code) => code switch
    {
        Success => "Success",
        GeneralError => "General error",
        InvalidArguments => "Invalid arguments",
        AuthenticationError => "Authentication error",
        NetworkError => "Network error",
        CopilotError => "Copilot SDK error",
        ConfigurationError => "Configuration error",
        Cancelled => "Operation cancelled",
        _ => $"Unknown error (code: {code})"
    };
}
