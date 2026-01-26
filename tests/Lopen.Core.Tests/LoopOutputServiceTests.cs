using Shouldly;
using Spectre.Console.Testing;

namespace Lopen.Core.Tests;

public class LoopOutputServiceTests
{
    private readonly TestConsole _testConsole;
    private readonly ConsoleOutput _consoleOutput;
    private readonly LoopOutputService _service;

    public LoopOutputServiceTests()
    {
        _testConsole = new TestConsole();
        _consoleOutput = new ConsoleOutput(_testConsole);
        _service = new LoopOutputService(_consoleOutput);
    }

    [Fact]
    public void IterationCount_InitiallyZero()
    {
        _service.IterationCount.ShouldBe(0);
    }

    [Fact]
    public void WriteIterationComplete_IncrementsCounter()
    {
        _service.WriteIterationComplete();
        _service.IterationCount.ShouldBe(1);

        _service.WriteIterationComplete();
        _service.IterationCount.ShouldBe(2);
    }

    [Fact]
    public void WriteIterationComplete_WritesMessage()
    {
        _service.WriteIterationComplete();

        var output = _testConsole.Output;
        output.ShouldContain("Completed iteration 1");
    }

    [Fact]
    public void WritePhaseHeader_WritesPhase()
    {
        _service.WritePhaseHeader("PLAN");

        var output = _testConsole.Output;
        output.ShouldContain("PLAN");
    }

    [Fact]
    public void ResetIterationCount_ResetsToZero()
    {
        _service.WriteIterationComplete();
        _service.WriteIterationComplete();
        _service.IterationCount.ShouldBe(2);

        _service.ResetIterationCount();

        _service.IterationCount.ShouldBe(0);
    }

    [Fact]
    public void Info_DelegatesToConsoleOutput()
    {
        _service.Info("Test message");

        var output = _testConsole.Output;
        output.ShouldContain("Test message");
    }

    [Fact]
    public void Success_DelegatesToConsoleOutput()
    {
        _service.Success("Success message");

        var output = _testConsole.Output;
        output.ShouldContain("Success message");
    }

    [Fact]
    public void Error_DelegatesToConsoleOutput()
    {
        _service.Error("Error message");

        var output = _testConsole.Output;
        output.ShouldContain("Error message");
    }

    [Fact]
    public void Warning_DelegatesToConsoleOutput()
    {
        _service.Warning("Warning message");

        var output = _testConsole.Output;
        output.ShouldContain("Warning message");
    }
}
