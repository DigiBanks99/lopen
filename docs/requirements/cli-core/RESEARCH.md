# CLI Core Research

> Research findings for JTBD-001 (Project Setup) and REQ-001/REQ-002 implementation

## 1. .NET 10 Solution Structure

### Commands to Create Solution

```bash
# Create solution
dotnet new sln -n Lopen

# Create projects
dotnet new console -n Lopen.Cli -f net10.0
dotnet new classlib -n Lopen.Core -f net10.0
dotnet new xunit -n Lopen.Core.Tests -f net10.0
dotnet new xunit -n Lopen.Cli.Tests -f net10.0

# Add projects to solution
dotnet sln add src/Lopen.Cli/Lopen.Cli.csproj
dotnet sln add src/Lopen.Core/Lopen.Core.csproj
dotnet sln add tests/Lopen.Core.Tests/Lopen.Core.Tests.csproj
dotnet sln add tests/Lopen.Cli.Tests/Lopen.Cli.Tests.csproj

# Add references
dotnet add src/Lopen.Cli/Lopen.Cli.csproj reference src/Lopen.Core/Lopen.Core.csproj
dotnet add tests/Lopen.Core.Tests/Lopen.Core.Tests.csproj reference src/Lopen.Core/Lopen.Core.csproj
dotnet add tests/Lopen.Cli.Tests/Lopen.Cli.Tests.csproj reference src/Lopen.Cli/Lopen.Cli.csproj
```

### Directory.Build.props

Place in repository root for shared settings:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

---

## 2. System.CommandLine Setup

### NuGet Package

```bash
dotnet add package System.CommandLine
```

### Root Command with Version and Help

```csharp
using System.CommandLine;

var rootCommand = new RootCommand("Lopen - GitHub Copilot CLI")
{
    // Options and subcommands added here
};

// Version comes automatically with RootCommand
// Help (--help, -h, -?) comes automatically

// Parse and invoke
return rootCommand.Parse(args).Invoke();
```

### Subcommand Pattern

```csharp
// Create auth command with subcommands
var authCommand = new Command("auth", "Authentication commands");

var loginCommand = new Command("login", "Login to GitHub Copilot");
loginCommand.SetAction(parseResult => { /* login logic */ return 0; });
authCommand.Subcommands.Add(loginCommand);

var statusCommand = new Command("status", "Check authentication status");
statusCommand.SetAction(parseResult => { /* status logic */ return 0; });
authCommand.Subcommands.Add(statusCommand);

rootCommand.Subcommands.Add(authCommand);
```

### Global Options

```csharp
// Format option available to all commands
var formatOption = new Option<string>("--format", "Output format")
{
    DefaultValueFactory = _ => "text"
};
formatOption.AcceptOnlyFromAmong("text", "json");
rootCommand.Options.Add(formatOption);
```

---

## 3. Spectre.Console Integration

### NuGet Package

```bash
dotnet add package Spectre.Console
```

### Basic Usage

```csharp
using Spectre.Console;

// Colored output
AnsiConsole.MarkupLine("[green]Success![/] Operation completed.");
AnsiConsole.MarkupLine("[red]Error:[/] Something went wrong.");

// Respecting NO_COLOR
if (Environment.GetEnvironmentVariable("NO_COLOR") != null)
{
    AnsiConsole.Profile.Capabilities.Ansi = false;
}
```

### Graceful Degradation

Spectre.Console auto-detects terminal capabilities. For manual control:

```csharp
var console = AnsiConsole.Create(new AnsiConsoleSettings
{
    Ansi = AnsiSupport.Detect,
    ColorSystem = ColorSystemSupport.Detect
});
```

---

## 4. xUnit with FluentAssertions

### NuGet Packages

```bash
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
dotnet add package FluentAssertions
dotnet add package coverlet.collector
```

### Example Test

```csharp
using FluentAssertions;
using Xunit;

public class VersionServiceTests
{
    [Fact]
    public void GetVersion_ReturnsSemanticVersion()
    {
        var service = new VersionService();
        
        var version = service.GetVersion();
        
        version.Should().MatchRegex(@"^\d+\.\d+\.\d+$");
    }
}
```

### Run with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

## 5. Single-File Publishing

### .csproj Properties

```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  <PublishTrimmed>true</PublishTrimmed>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
</PropertyGroup>
```

### Publish Commands

```bash
# Windows x64
dotnet publish -c Release -r win-x64 --self-contained

# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained

# macOS x64
dotnet publish -c Release -r osx-x64 --self-contained

# macOS ARM64 (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained
```

---

## 6. Version Information

### Embedding Version in Assembly

```xml
<!-- In .csproj -->
<PropertyGroup>
  <Version>0.1.0</Version>
  <AssemblyVersion>0.1.0.0</AssemblyVersion>
  <FileVersion>0.1.0.0</FileVersion>
</PropertyGroup>
```

### Reading Version at Runtime

```csharp
var version = typeof(Program).Assembly
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion ?? "unknown";
```

---

## References

- [System.CommandLine Tutorial](https://learn.microsoft.com/en-us/dotnet/standard/commandline/get-started-tutorial)
- [Spectre.Console Documentation](https://spectreconsole.net)
- [.NET Publishing](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
