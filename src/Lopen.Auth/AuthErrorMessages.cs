namespace Lopen.Auth;

/// <summary>
/// Centralised auth error messages following the what/why/how-to-fix pattern.
/// </summary>
internal static class AuthErrorMessages
{
    public const string NotAuthenticated =
        "Not authenticated. No valid credentials found.\n"
        + "Why: Neither environment variables nor stored credentials are available.\n"
        + "Fix: Run 'lopen auth login' for interactive login, or set the GH_TOKEN environment variable.";

    public const string HeadlessLoginNotSupported =
        "Interactive login is not available in non-interactive environments.\n"
        + "Why: Device flow requires a TTY for user interaction.\n"
        + "Fix: Set the GH_TOKEN environment variable with a fine-grained PAT that has the 'Copilot Requests' permission.\n"
        + "See: https://github.com/settings/personal-access-tokens/new";

    public const string GhCliNotFound =
        "Interactive login requires the GitHub CLI (gh).\n"
        + "Why: Lopen delegates device flow authentication to the gh CLI.\n"
        + "Fix: Install gh (https://cli.github.com) or set the GH_TOKEN environment variable.";

    public const string LoginFailed =
        "Authentication failed.\n"
        + "Why: The gh auth login process did not complete successfully.\n"
        + "Fix: Try again with 'lopen auth login', or set the GH_TOKEN environment variable.";

    public const string InvalidCredentials =
        "Credentials are invalid or expired.\n"
        + "Why: The stored token was rejected by GitHub.\n"
        + "Fix: Run 'lopen auth login' to re-authenticate, or update the GH_TOKEN environment variable.";

    public const string InvalidPat =
        "Personal access token is invalid or lacks required permissions.\n"
        + "Why: The token must be a fine-grained PAT with the 'Copilot Requests' permission enabled.\n"
        + "Fix: Create a new PAT at https://github.com/settings/personal-access-tokens/new with the 'Copilot Requests' permission.";

    public const string PreFlightFailed =
        "Pre-flight authentication check failed.\n"
        + "Why: No valid credentials found to proceed with the workflow.\n"
        + "Fix: Run 'lopen auth login' or set the GH_TOKEN environment variable before starting a workflow.";

    public static string EnvVarStillSet(string variableName) =>
        $"Logged out of gh CLI, but the {variableName} environment variable is still set.\n"
        + $"Why: Environment variable authentication takes precedence over stored credentials.\n"
        + $"Fix: Unset {variableName} to fully log out, or leave it set to continue using token-based authentication.";
}
