using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Lopen.Cli.Tests;

public class ReplCommandTests
{
    [Fact]
    public void ReplCommand_Help_ShowsReplDescription()
    {
        var output = RunCli(["repl", "--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("interactive");
    }

    [Fact]
    public void ReplCommand_AppearsInHelpList()
    {
        var output = RunCli(["--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("repl");
    }

    [Fact]
    public void RootCommand_WithNoArgs_MentionsRepl()
    {
        var output = RunCli([]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("repl");
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
