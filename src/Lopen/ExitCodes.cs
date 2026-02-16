namespace Lopen.Commands;

/// <summary>
/// Standard exit codes for the Lopen CLI.
/// </summary>
public static class ExitCodes
{
    /// <summary>Command completed successfully.</summary>
    public const int Success = 0;

    /// <summary>Command failed due to an error.</summary>
    public const int Failure = 1;

    /// <summary>User intervention required (headless + unattended only).</summary>
    public const int UserInterventionRequired = 2;
}
