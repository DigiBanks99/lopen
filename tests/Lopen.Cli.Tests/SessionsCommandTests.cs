using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Lopen.Cli.Tests;

public class SessionsCommandTests
{
    [Fact]
    public void SessionsCommand_Help_ShowsDescription()
    {
        var output = RunCli(["sessions", "--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("session");
    }

    [Fact]
    public void SessionsCommand_AppearsInHelpList()
    {
        var output = RunCli(["--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("sessions");
    }

    [Fact]
    public void SessionsCommand_Help_ShowsListSubcommand()
    {
        var output = RunCli(["sessions", "--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("list");
    }

    [Fact]
    public void SessionsCommand_Help_ShowsDeleteSubcommand()
    {
        var output = RunCli(["sessions", "--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("delete");
    }

    [Fact]
    public void SessionsList_Help_ShowsDescription()
    {
        var output = RunCli(["sessions", "list", "--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("List");
    }

    [Fact]
    public void SessionsDelete_Help_ShowsSessionIdArgument()
    {
        var output = RunCli(["sessions", "delete", "--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("session-id");
    }

    [Fact]
    public void Help_Sessions_ShowsSubcommands()
    {
        var output = RunCli(["help", "sessions"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("list");
        output.StandardOutput.ShouldContain("delete");
    }

    [Fact]
    public void ChatCommand_Help_ShowsResumeOption()
    {
        var output = RunCli(["chat", "--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("--resume");
        output.StandardOutput.ShouldContain("-r");
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
