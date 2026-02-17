using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Lopen.Auth;

/// <summary>
/// Adapter that delegates authentication operations to the GitHub CLI (gh).
/// </summary>
internal sealed partial class GhCliAdapter : IGhCliAdapter
{
    private const string GhCommand = "gh";
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<GhCliAdapter> _logger;
    private readonly Func<ProcessStartInfo, Process?> _processStarter;

    public GhCliAdapter(ILogger<GhCliAdapter> logger)
        : this(logger, startInfo => Process.Start(startInfo))
    {
    }

    /// <summary>
    /// Constructor accepting a custom process starter for testability.
    /// </summary>
    internal GhCliAdapter(ILogger<GhCliAdapter> logger, Func<ProcessStartInfo, Process?> processStarter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processStarter = processStarter ?? throw new ArgumentNullException(nameof(processStarter));
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RunAsync("--version", redirectOutput: true, cancellationToken: cancellationToken);
            return result.ExitCode == 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "gh CLI not available");
            return false;
        }
    }

    public async Task<string> LoginAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting gh auth login with web browser flow");

        // Use --web to open browser for device flow, --git-protocol https for HTTPS
        var result = await RunAsync(
            "auth login --git-protocol https --web",
            redirectOutput: false,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogError("gh auth login failed with exit code {ExitCode}: {Error}", result.ExitCode, result.StdErr);
            throw new AuthenticationException(AuthErrorMessages.LoginFailed);
        }

        // Verify login by checking status
        var status = await GetStatusAsync(cancellationToken);
        if (status is null)
        {
            throw new AuthenticationException(AuthErrorMessages.LoginFailed);
        }

        _logger.LogInformation("Successfully authenticated as {Username}", status.Username);
        return status.Username;
    }

    public async Task<GhAuthStatusInfo?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync("auth status", redirectOutput: true, cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogDebug("gh auth status returned exit code {ExitCode}", result.ExitCode);
            return null;
        }

        // Parse combined output (gh writes status to both stdout and stderr depending on version)
        var output = string.IsNullOrWhiteSpace(result.StdOut) ? result.StdErr : result.StdOut;
        return ParseStatusOutput(output);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running gh auth logout");

        var result = await RunAsync(
            "auth logout --hostname github.com",
            redirectOutput: true,
            input: "Y\n",
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            _logger.LogWarning("gh auth logout returned exit code {ExitCode}: {Error}", result.ExitCode, result.StdErr);
        }
    }

    public async Task<bool> ValidateCredentialsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RunAsync("api user --jq .login", redirectOutput: true, cancellationToken: cancellationToken);
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StdOut);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Credential validation failed");
            return false;
        }
    }

    internal static GhAuthStatusInfo? ParseStatusOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        // Match patterns like "Logged in to github.com account USERNAME" or "account USERNAME ("
        var accountMatch = AccountPattern().Match(output);
        if (!accountMatch.Success)
        {
            return null;
        }

        var username = accountMatch.Groups["username"].Value;
        var isActive = output.Contains("Active account: true", StringComparison.OrdinalIgnoreCase);

        // Extract token scopes if present
        string? scopes = null;
        var scopesMatch = ScopesPattern().Match(output);
        if (scopesMatch.Success)
        {
            scopes = scopesMatch.Groups["scopes"].Value.Trim();
        }

        return new GhAuthStatusInfo(username, isActive, scopes);
    }

    private async Task<GhProcessResult> RunAsync(
        string arguments,
        bool redirectOutput,
        string? input = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GhCommand,
            Arguments = arguments,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput,
            RedirectStandardInput = input is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        _logger.LogDebug("Running: {Command} {Arguments}", GhCommand, arguments);

        var process = _processStarter(startInfo);
        if (process is null)
        {
            throw new AuthenticationException(AuthErrorMessages.GhCliNotFound);
        }

        try
        {
            if (input is not null)
            {
                await process.StandardInput.WriteAsync(input);
                process.StandardInput.Close();
            }

            string stdOut = "";
            string stdErr = "";

            if (redirectOutput)
            {
                var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
                stdOut = await stdOutTask;
                stdErr = await stdErrTask;
            }

            await process.WaitForExitAsync(cancellationToken).WaitAsync(ProcessTimeout, cancellationToken);

            return new GhProcessResult(process.ExitCode, stdOut, stdErr);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill();
            }

            process.Dispose();
        }
    }

    [GeneratedRegex(@"account\s+(?<username>\S+)", RegexOptions.IgnoreCase)]
    private static partial Regex AccountPattern();

    [GeneratedRegex(@"Token scopes:\s*(?<scopes>.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ScopesPattern();

    internal sealed record GhProcessResult(int ExitCode, string StdOut, string StdErr);
}
