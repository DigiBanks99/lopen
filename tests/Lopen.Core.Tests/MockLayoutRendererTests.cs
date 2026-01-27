using Lopen.Core;
using Shouldly;
using Spectre.Console;

namespace Lopen.Core.Tests;

public class MockLayoutRendererTests
{
    [Fact]
    public void RenderSplitLayout_RecordsCall()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();
        var main = new Text("Main content");
        var panel = new Text("Side panel");

        // Act
        renderer.RenderSplitLayout(main, panel);

        // Assert
        renderer.SplitLayoutCalls.Count.ShouldBe(1);
        renderer.SplitLayoutCalls[0].MainContent.ShouldBe(main);
        renderer.SplitLayoutCalls[0].SidePanel.ShouldBe(panel);
    }

    [Fact]
    public void RenderSplitLayout_RecordsConfig()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();
        var main = new Text("Main");
        var config = new SplitLayoutConfig { MinWidthForSplit = 80 };

        // Act
        renderer.RenderSplitLayout(main, null, config);

        // Assert
        renderer.SplitLayoutCalls[0].Config.MinWidthForSplit.ShouldBe(80);
    }

    [Fact]
    public void RenderSplitLayout_WasSplit_TrueWhenWideEnough()
    {
        // Arrange
        var renderer = new MockLayoutRenderer { SimulatedWidth = 120 };
        var main = new Text("Main");
        var panel = new Text("Panel");

        // Act
        renderer.RenderSplitLayout(main, panel);

        // Assert
        renderer.SplitLayoutCalls[0].WasSplit.ShouldBeTrue();
    }

    [Fact]
    public void RenderSplitLayout_WasSplit_FalseWhenNarrow()
    {
        // Arrange
        var renderer = new MockLayoutRenderer { SimulatedWidth = 60 };
        var main = new Text("Main");
        var panel = new Text("Panel");

        // Act
        renderer.RenderSplitLayout(main, panel);

        // Assert
        renderer.SplitLayoutCalls[0].WasSplit.ShouldBeFalse();
    }

    [Fact]
    public void RenderSplitLayout_WasSplit_FalseWhenNoPanel()
    {
        // Arrange
        var renderer = new MockLayoutRenderer { SimulatedWidth = 120 };
        var main = new Text("Main");

        // Act
        renderer.RenderSplitLayout(main);

        // Assert
        renderer.SplitLayoutCalls[0].WasSplit.ShouldBeFalse();
    }

    [Fact]
    public void RenderTaskPanel_RecordsCall()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();
        var tasks = new List<TaskItem>
        {
            new() { Name = "Task 1", Status = TaskStatus.Completed },
            new() { Name = "Task 2", Status = TaskStatus.InProgress }
        };

        // Act
        renderer.RenderTaskPanel(tasks, "Progress");

        // Assert
        renderer.TaskPanelCalls.Count.ShouldBe(1);
        renderer.TaskPanelCalls[0].Tasks.Count.ShouldBe(2);
        renderer.TaskPanelCalls[0].Title.ShouldBe("Progress");
    }

    [Fact]
    public void RenderTaskPanel_RecordsTaskDetails()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();
        var tasks = new List<TaskItem>
        {
            new() { Name = "Connect", Status = TaskStatus.Completed },
            new() { Name = "Authenticate", Status = TaskStatus.InProgress },
            new() { Name = "Process", Status = TaskStatus.Pending }
        };

        // Act
        renderer.RenderTaskPanel(tasks);

        // Assert
        renderer.TaskPanelCalls[0].Tasks[0].Name.ShouldBe("Connect");
        renderer.TaskPanelCalls[0].Tasks[0].Status.ShouldBe(TaskStatus.Completed);
        renderer.TaskPanelCalls[0].Tasks[1].Status.ShouldBe(TaskStatus.InProgress);
        renderer.TaskPanelCalls[0].Tasks[2].Status.ShouldBe(TaskStatus.Pending);
    }

    [Fact]
    public void RenderContextPanel_RecordsCall()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();
        var data = new Dictionary<string, string>
        {
            ["Model"] = "claude",
            ["Tokens"] = "1.2K"
        };

        // Act
        renderer.RenderContextPanel(data, "Session");

        // Assert
        renderer.ContextPanelCalls.Count.ShouldBe(1);
        renderer.ContextPanelCalls[0].Title.ShouldBe("Session");
        renderer.ContextPanelCalls[0].Data.ShouldContainKey("Model");
        renderer.ContextPanelCalls[0].Data["Tokens"].ShouldBe("1.2K");
    }

    [Fact]
    public void Reset_ClearsAllCalls()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();
        renderer.RenderSplitLayout(new Text("Main"));
        renderer.RenderTaskPanel(new List<TaskItem>());
        renderer.RenderContextPanel(new Dictionary<string, string>(), "Test");

        // Act
        renderer.Reset();

        // Assert
        renderer.SplitLayoutCalls.Count.ShouldBe(0);
        renderer.TaskPanelCalls.Count.ShouldBe(0);
        renderer.ContextPanelCalls.Count.ShouldBe(0);
    }

    [Fact]
    public void TerminalWidth_ReturnsSimulatedWidth()
    {
        // Arrange
        var renderer = new MockLayoutRenderer { SimulatedWidth = 80 };

        // Act & Assert
        renderer.TerminalWidth.ShouldBe(80);
    }

    [Fact]
    public void SimulatedWidth_DefaultIs120()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();

        // Act & Assert
        renderer.SimulatedWidth.ShouldBe(120);
    }

    [Fact]
    public void RenderSplitLayout_DefaultConfig_UsesMinWidth100()
    {
        // Arrange
        var renderer = new MockLayoutRenderer { SimulatedWidth = 99 };
        var main = new Text("Main");
        var panel = new Text("Panel");

        // Act
        renderer.RenderSplitLayout(main, panel);

        // Assert
        renderer.SplitLayoutCalls[0].WasSplit.ShouldBeFalse();

        // At exactly 100 chars, should split
        renderer.SimulatedWidth = 100;
        renderer.RenderSplitLayout(main, panel);
        renderer.SplitLayoutCalls[1].WasSplit.ShouldBeTrue();
    }

    [Fact]
    public void RenderTaskPanel_ReturnsRenderable()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();
        var tasks = new List<TaskItem> { new() { Name = "Test" } };

        // Act
        var result = renderer.RenderTaskPanel(tasks);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void RenderContextPanel_ReturnsRenderable()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();
        var data = new Dictionary<string, string> { ["Key"] = "Value" };

        // Act
        var result = renderer.RenderContextPanel(data);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task StartLiveLayoutAsync_RecordsCall()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();
        var main = new Text("Main");
        var panel = new Text("Panel");

        // Act
        await using var context = await renderer.StartLiveLayoutAsync(main, panel);

        // Assert
        renderer.LiveLayoutCalls.Count.ShouldBe(1);
        renderer.LiveLayoutCalls[0].InitialMain.ShouldBe(main);
        renderer.LiveLayoutCalls[0].InitialPanel.ShouldBe(panel);
    }

    [Fact]
    public async Task StartLiveLayoutAsync_ReturnsActiveContext()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();
        var main = new Text("Main");

        // Act
        await using var context = await renderer.StartLiveLayoutAsync(main);

        // Assert
        context.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task StartLiveLayoutAsync_SetsLastLiveContext()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();
        var main = new Text("Main");

        // Act
        await using var context = await renderer.StartLiveLayoutAsync(main);

        // Assert
        renderer.LastLiveContext.ShouldNotBeNull();
        renderer.LastLiveContext.ShouldBeSameAs(context);
    }

    [Fact]
    public async Task MockLiveLayoutContext_RecordsMainUpdates()
    {
        // Arrange
        var context = new MockLiveLayoutContext();
        var content1 = new Text("Update 1");
        var content2 = new Text("Update 2");

        // Act
        context.UpdateMain(content1);
        context.UpdateMain(content2);

        // Assert
        context.MainUpdates.Count.ShouldBe(2);
        context.MainUpdates[0].ShouldBe(content1);
        context.MainUpdates[1].ShouldBe(content2);
    }

    [Fact]
    public async Task MockLiveLayoutContext_RecordsPanelUpdates()
    {
        // Arrange
        var context = new MockLiveLayoutContext();
        var content = new Text("Panel update");

        // Act
        context.UpdatePanel(content);

        // Assert
        context.PanelUpdates.Count.ShouldBe(1);
        context.PanelUpdates[0].ShouldBe(content);
    }

    [Fact]
    public async Task MockLiveLayoutContext_CountsRefreshCalls()
    {
        // Arrange
        var context = new MockLiveLayoutContext();

        // Act
        context.Refresh();
        context.Refresh();
        context.Refresh();

        // Assert
        context.RefreshCount.ShouldBe(3);
    }

    [Fact]
    public async Task MockLiveLayoutContext_DisposeSetsInactive()
    {
        // Arrange
        var context = new MockLiveLayoutContext();

        // Act
        await context.DisposeAsync();

        // Assert
        context.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task MockLiveLayoutContext_UpdatesIgnoredAfterDispose()
    {
        // Arrange
        var context = new MockLiveLayoutContext();

        // Act
        await context.DisposeAsync();
        context.UpdateMain(new Text("Should be ignored"));
        context.UpdatePanel(new Text("Should be ignored"));
        context.Refresh();

        // Assert
        context.MainUpdates.Count.ShouldBe(0);
        context.PanelUpdates.Count.ShouldBe(0);
        context.RefreshCount.ShouldBe(0);
    }

    [Fact]
    public async Task Reset_ClearsLiveLayoutCallsAndContext()
    {
        // Arrange
        var renderer = new MockLayoutRenderer();
        await renderer.StartLiveLayoutAsync(new Text("Main"));

        // Act
        renderer.Reset();

        // Assert
        renderer.LiveLayoutCalls.Count.ShouldBe(0);
        renderer.LastLiveContext.ShouldBeNull();
    }
}
