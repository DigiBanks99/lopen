using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Lopen.Cli.Tests;

public class AuthCommandTests
{
    [Fact]
    public void AuthStatus_NotAuthenticated_ShowsNotAuthenticated()
    {
        var output = RunCli(["auth", "status"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("Not authenticated");
    }

    [Fact]
    public void AuthLogout_ClearsCredentials()
    {
        var output = RunCli(["auth", "logout"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("Credentials cleared");
    }

    [Fact]
    public void AuthLogin_WithoutToken_ShowsDeviceFlowOrInstructions()
    {
        // With OAuth config, it starts device flow; without, it shows token instructions
        // Device flow will wait for user input, so we use a short timeout
        var output = RunCli(["auth", "login"], timeoutMs: 5000);

        // Exit code can be:
        // 0 - Success (no OAuth config, shows instructions)
        // 3 - Auth error (device flow error)
        // -1 - Timeout (device flow waiting for user, which is expected)
        output.ExitCode.ShouldBeOneOf(0, 3, -1);
        
        // Either device flow message or token instruction should appear
        (output.StandardOutput.Contains("device") || 
         output.StandardOutput.Contains("token") ||
         output.StandardOutput.Contains("browser") ||
         output.StandardOutput.Contains("Visit") ||
         output.StandardOutput.Contains("authorization") ||
         output.StandardOutput.Contains("code")).ShouldBeTrue($"Unexpected output: {output.StandardOutput}");
    }

    [Fact]
    public void Help_Auth_ShowsSubcommands()
    {
        var output = RunCli(["help", "auth"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("login");
        output.StandardOutput.ShouldContain("status");
        output.StandardOutput.ShouldContain("logout");
    }

    private static CliOutput RunCli(string[] args, int timeoutMs = 30000)
    {
        var cliProjectPath = GetCliProjectPath();
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{cliProjectPath}\" --no-build -- {string.Join(" ", args)}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)!;
        
        // Read output with timeout
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        
        if (!process.WaitForExit(timeoutMs))
        {
            process.Kill(true);
            return new CliOutput(-1, stdoutTask.Result, stderrTask.Result + "\n[Process killed due to timeout]");
        }
        
        return new CliOutput(process.ExitCode, stdoutTask.Result, stderrTask.Result);
    }

    private static string GetCliProjectPath()
    {
        var testDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "src", "Lopen.Cli", "Lopen.Cli.csproj");
    }

    private record CliOutput(int ExitCode, string StandardOutput, string StandardError);
}
