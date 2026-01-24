using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace Lopen.Cli.Tests;

public class ChatCommandTests
{
    [Fact]
    public void ChatCommand_Help_ShowsDescription()
    {
        var output = RunCli(["chat", "--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("AI chat");
    }

    [Fact]
    public void ChatCommand_AppearsInHelpList()
    {
        var output = RunCli(["--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("chat");
    }

    [Fact]
    public void ChatCommand_Help_ShowsModelOption()
    {
        var output = RunCli(["chat", "--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("--model");
        output.StandardOutput.Should().Contain("-m");
    }

    [Fact]
    public void ChatCommand_Help_ShowsStreamingOption()
    {
        var output = RunCli(["chat", "--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("--streaming");
        output.StandardOutput.Should().Contain("-s");
    }

    [Fact]
    public void ChatCommand_Help_ShowsPromptArgument()
    {
        var output = RunCli(["chat", "--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("prompt");
    }

    [Fact]
    public void Help_Chat_ShowsDescription()
    {
        var output = RunCli(["help", "chat"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("chat");
        output.StandardOutput.Should().Contain("AI");
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
