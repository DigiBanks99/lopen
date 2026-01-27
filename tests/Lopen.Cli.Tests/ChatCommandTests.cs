using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Lopen.Cli.Tests;

public class ChatCommandTests
{
    [Fact]
    public void ChatCommand_Help_ShowsDescription()
    {
        var output = RunCli(["chat", "--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("AI chat");
    }

    [Fact]
    public void ChatCommand_AppearsInHelpList()
    {
        var output = RunCli(["--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("chat");
    }

    [Fact]
    public void ChatCommand_Help_ShowsModelOption()
    {
        var output = RunCli(["chat", "--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("--model");
        output.StandardOutput.ShouldContain("-m");
    }

    [Fact]
    public void ChatCommand_Help_ShowsStreamingOption()
    {
        var output = RunCli(["chat", "--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("--streaming");
        output.StandardOutput.ShouldContain("-s");
    }

    [Fact]
    public void ChatCommand_Help_ShowsPromptArgument()
    {
        var output = RunCli(["chat", "--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("prompt");
    }

    [Fact]
    public void Help_Chat_ShowsDescription()
    {
        var output = RunCli(["help", "chat"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("chat");
        output.StandardOutput.ShouldContain("AI");
    }

    [Fact]
    public void ChatCommand_Help_ShowsNoHeaderOption()
    {
        var output = RunCli(["chat", "--help"]);

        output.ExitCode.ShouldBe(0);
        output.StandardOutput.ShouldContain("--no-header");
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
