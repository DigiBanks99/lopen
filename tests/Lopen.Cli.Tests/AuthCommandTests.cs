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
    public void AuthLogin_WithoutToken_ShowsInstructions()
    {
        var output = RunCli(["auth", "login"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("token");
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

    private static CliOutput RunCli(string[] args)
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
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new CliOutput(process.ExitCode, stdout, stderr);
    }

    private static string GetCliProjectPath()
    {
        var testDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "src", "Lopen.Cli", "Lopen.Cli.csproj");
    }

    private record CliOutput(int ExitCode, string StandardOutput, string StandardError);
}
