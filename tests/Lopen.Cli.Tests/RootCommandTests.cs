using System.CommandLine;
using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace Lopen.Cli.Tests;

public class RootCommandTests
{
    [Fact]
    public void RootCommand_WithNoArgs_ShowsHelpHint()
    {
        var output = RunCli([]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("--help");
    }

    [Fact]
    public void RootCommand_WithHelp_ShowsDescription()
    {
        var output = RunCli(["--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("Lopen");
        output.StandardOutput.Should().Contain("GitHub Copilot CLI");
    }

    [Fact]
    public void RootCommand_WithVersion_ShowsVersion()
    {
        var output = RunCli(["--version"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().MatchRegex(@"\d+\.\d+\.\d+");
    }

    [Fact]
    public void RootCommand_Help_ListsVersionCommand()
    {
        var output = RunCli(["--help"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("version");
    }

    private static CliOutput RunCli(string[] args)
    {
        // Get the path to the CLI project
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
        // Navigate from test output to CLI project
        var testDir = AppContext.BaseDirectory;
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "src", "Lopen.Cli", "Lopen.Cli.csproj");
    }

    private record CliOutput(int ExitCode, string StandardOutput, string StandardError);
}

public class VersionCommandTests
{
    [Fact]
    public void VersionCommand_Text_ShowsLopenVersion()
    {
        var output = RunCli(["version"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().MatchRegex(@"lopen version \d+\.\d+\.\d+");
    }

    [Fact]
    public void VersionCommand_JsonFormat_ReturnsValidJson()
    {
        var output = RunCli(["version", "--format", "json"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("\"version\"");
        output.StandardOutput.Should().MatchRegex(@"\{""version"":""\d+\.\d+\.\d+""\}");
    }

    [Fact]
    public void VersionCommand_ShortFormatFlag_Works()
    {
        var output = RunCli(["version", "-f", "json"]);

        output.ExitCode.Should().Be(0);
        output.StandardOutput.Should().Contain("\"version\"");
    }

    [Fact]
    public void VersionCommand_InvalidFormat_ShowsError()
    {
        var output = RunCli(["version", "--format", "xml"]);

        output.ExitCode.Should().NotBe(0);
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
