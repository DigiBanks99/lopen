using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace Lopen.Cli.Tests;

public class SessionsCommandTests
{
    [Fact]
    public void SessionsCommand_Help_ShowsDescription()
    {
        var output = RunCli(["sessions", "--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("session");
    }

    [Fact]
    public void SessionsCommand_AppearsInHelpList()
    {
        var output = RunCli(["--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("sessions");
    }

    [Fact]
    public void SessionsCommand_Help_ShowsListSubcommand()
    {
        var output = RunCli(["sessions", "--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("list");
    }

    [Fact]
    public void SessionsCommand_Help_ShowsDeleteSubcommand()
    {
        var output = RunCli(["sessions", "--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("delete");
    }

    [Fact]
    public void SessionsList_Help_ShowsDescription()
    {
        var output = RunCli(["sessions", "list", "--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("List");
    }

    [Fact]
    public void SessionsDelete_Help_ShowsSessionIdArgument()
    {
        var output = RunCli(["sessions", "delete", "--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("session-id");
    }

    [Fact]
    public void Help_Sessions_ShowsSubcommands()
    {
        var output = RunCli(["help", "sessions"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("list");
        output.StandardOutput.Should().Contain("delete");
    }

    [Fact]
    public void ChatCommand_Help_ShowsResumeOption()
    {
        var output = RunCli(["chat", "--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("--resume");
        output.StandardOutput.Should().Contain("-r");
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
