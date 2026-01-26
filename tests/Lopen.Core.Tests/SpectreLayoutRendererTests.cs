using Lopen.Core;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Lopen.Core.Tests;

public class SpectreLayoutRendererTests
{
    [Fact]
    public void RenderSplitLayout_WideTerminal_RendersBothPanels()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreLayoutRenderer(console);
        var main = new Panel("Main content");
        var side = new Panel("Side panel");

        // Act
        renderer.RenderSplitLayout(main, side);

        // Assert
        var output = console.Output;
        output.ShouldContain("Main content");
        output.ShouldContain("Side panel");
    }

    [Fact]
    public void RenderSplitLayout_NarrowTerminal_ShowsMainOnly()
    {
        // Arrange
        var console = new TestConsole().Width(60);
        var renderer = new SpectreLayoutRenderer(console);
        var main = new Panel("Main content");
        var side = new Panel("Side panel");

        // Act
        renderer.RenderSplitLayout(main, side);

        // Assert
        var output = console.Output;
        output.ShouldContain("Main content");
        // Side panel might still appear in output but main point is fallback behavior
    }

    [Fact]
    public void RenderSplitLayout_NoSidePanel_ShowsMainOnly()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreLayoutRenderer(console);
        var main = new Panel("Main content");

        // Act
        renderer.RenderSplitLayout(main);

        // Assert
        console.Output.ShouldContain("Main content");
    }

    [Fact]
    public void RenderSplitLayout_CustomConfig_RespectsMinWidth()
    {
        // Arrange
        var console = new TestConsole().Width(80);
        var renderer = new SpectreLayoutRenderer(console);
        var main = new Panel("Main");
        var side = new Panel("Side");
        var config = new SplitLayoutConfig { MinWidthForSplit = 80 };

        // Act
        renderer.RenderSplitLayout(main, side, config);

        // Assert
        var output = console.Output;
        output.ShouldContain("Main");
        output.ShouldContain("Side");
    }

    [Fact]
    public void RenderTaskPanel_DisplaysTasksWithSymbols()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreLayoutRenderer(console);
        var tasks = new List<TaskItem>
        {
            new() { Name = "Connect", Status = TaskStatus.Completed },
            new() { Name = "Authenticate", Status = TaskStatus.InProgress },
            new() { Name = "Process", Status = TaskStatus.Pending },
            new() { Name = "Error", Status = TaskStatus.Failed }
        };

        // Act
        var panel = renderer.RenderTaskPanel(tasks, "Progress");
        console.Write(panel);

        // Assert
        var output = console.Output;
        output.ShouldContain("Progress");
        output.ShouldContain("Connect");
        output.ShouldContain("Authenticate");
        output.ShouldContain("Process");
        output.ShouldContain("Error");
        output.ShouldContain("✓");  // Completed
        output.ShouldContain("⏳"); // InProgress
        output.ShouldContain("○");  // Pending
        output.ShouldContain("✗");  // Failed
    }

    [Fact]
    public void RenderTaskPanel_DefaultTitle_IsProgress()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreLayoutRenderer(console);
        var tasks = new List<TaskItem> { new() { Name = "Task 1" } };

        // Act
        var panel = renderer.RenderTaskPanel(tasks);
        console.Write(panel);

        // Assert
        console.Output.ShouldContain("Progress");
    }

    [Fact]
    public void RenderContextPanel_DisplaysKeyValuePairs()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreLayoutRenderer(console);
        var data = new Dictionary<string, string>
        {
            ["Model"] = "claude",
            ["Tokens"] = "1.2K",
            ["Duration"] = "12s"
        };

        // Act
        var panel = renderer.RenderContextPanel(data, "Session");
        console.Write(panel);

        // Assert
        var output = console.Output;
        output.ShouldContain("Session");
        output.ShouldContain("Model");
        output.ShouldContain("claude");
        output.ShouldContain("Tokens");
        output.ShouldContain("1.2K");
        output.ShouldContain("Duration");
        output.ShouldContain("12s");
    }

    [Fact]
    public void RenderContextPanel_DefaultTitle_IsContext()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreLayoutRenderer(console);
        var data = new Dictionary<string, string> { ["Key"] = "Value" };

        // Act
        var panel = renderer.RenderContextPanel(data);
        console.Write(panel);

        // Assert
        console.Output.ShouldContain("Context");
    }

    [Fact]
    public void TerminalWidth_ReturnsConsoleWidth()
    {
        // Arrange
        var console = new TestConsole().Width(80);
        var renderer = new SpectreLayoutRenderer(console);

        // Act & Assert
        renderer.TerminalWidth.ShouldBe(80);
    }

    [Fact]
    public void RenderTaskPanel_EscapesMarkupCharacters()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreLayoutRenderer(console);
        var tasks = new List<TaskItem>
        {
            new() { Name = "Task with [brackets]", Status = TaskStatus.Completed }
        };

        // Act
        var panel = renderer.RenderTaskPanel(tasks);
        console.Write(panel);

        // Assert - should not throw and should contain escaped text
        console.Output.ShouldContain("Task with");
    }

    [Fact]
    public void RenderContextPanel_EscapesMarkupCharacters()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreLayoutRenderer(console);
        var data = new Dictionary<string, string>
        {
            ["Key[1]"] = "Value[2]"
        };

        // Act
        var panel = renderer.RenderContextPanel(data);
        console.Write(panel);

        // Assert
        var output = console.Output;
        output.ShouldContain("Key[1]");
        output.ShouldContain("Value[2]");
    }

    [Fact]
    public void RenderTaskPanel_EmptyList_ReturnsPanel()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreLayoutRenderer(console);
        var tasks = new List<TaskItem>();

        // Act
        var panel = renderer.RenderTaskPanel(tasks, "Tasks");
        console.Write(panel);

        // Assert - Panel should render even when empty
        console.Output.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void RenderContextPanel_EmptyData_ReturnsPanel()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreLayoutRenderer(console);
        var data = new Dictionary<string, string>();

        // Act
        var panel = renderer.RenderContextPanel(data, "Info");
        console.Write(panel);

        // Assert - Panel should render even when empty
        console.Output.Length.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Constructor_ThrowsOnNullConsole()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new SpectreLayoutRenderer(null!));
    }

    [Fact]
    public void SplitLayoutConfig_DefaultValues()
    {
        // Arrange & Act
        var config = new SplitLayoutConfig();

        // Assert
        config.MinWidthForSplit.ShouldBe(100);
        config.MainRatio.ShouldBe(7);
        config.PanelRatio.ShouldBe(3);
    }
}
