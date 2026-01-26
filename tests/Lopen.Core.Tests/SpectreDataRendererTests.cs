using Lopen.Core;
using Shouldly;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Lopen.Core.Tests;

public class SpectreDataRendererTests
{
    [Fact]
    public void RenderTable_DisplaysTableWithHeaders()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreDataRenderer(console);
        var items = new[] { new TestItem("Alice", 25), new TestItem("Bob", 30) };
        var config = new TableConfig<TestItem>
        {
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name },
                new() { Header = "Age", Selector = x => x.Age.ToString() }
            },
            ShowRowCount = false
        };

        // Act
        renderer.RenderTable(items, config);

        // Assert
        var output = console.Output;
        output.ShouldContain("Name");
        output.ShouldContain("Age");
        output.ShouldContain("Alice");
        output.ShouldContain("25");
        output.ShouldContain("Bob");
        output.ShouldContain("30");
    }

    [Fact]
    public void RenderTable_ShowsRowCount_WhenConfigured()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreDataRenderer(console);
        var items = new[] { new TestItem("Alice", 25), new TestItem("Bob", 30) };
        var config = new TableConfig<TestItem>
        {
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name }
            },
            ShowRowCount = true,
            RowCountFormat = "{0} users"
        };

        // Act
        renderer.RenderTable(items, config);

        // Assert
        console.Output.ShouldContain("2 users");
    }

    [Fact]
    public void RenderTable_EmptyItems_ShowsInfoMessage()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreDataRenderer(console);
        var items = Array.Empty<TestItem>();
        var config = new TableConfig<TestItem>
        {
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name }
            }
        };

        // Act
        renderer.RenderTable(items, config);

        // Assert
        console.Output.ShouldContain("No items to display");
    }

    [Fact]
    public void RenderTable_WithTitle_DisplaysTitle()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreDataRenderer(console);
        var items = new[] { new TestItem("Alice", 25) };
        var config = new TableConfig<TestItem>
        {
            Title = "User List",
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name }
            },
            ShowRowCount = false
        };

        // Act
        renderer.RenderTable(items, config);

        // Assert
        console.Output.ShouldContain("User List");
    }

    [Fact]
    public void RenderMetadata_DisplaysKeyValuePairs()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreDataRenderer(console);
        var data = new Dictionary<string, string>
        {
            ["Status"] = "Active",
            ["Version"] = "1.0.0"
        };

        // Act
        renderer.RenderMetadata(data, "App Info");

        // Assert
        var output = console.Output;
        output.ShouldContain("App Info");
        output.ShouldContain("Status");
        output.ShouldContain("Active");
        output.ShouldContain("Version");
        output.ShouldContain("1.0.0");
    }

    [Fact]
    public void RenderMetadata_EmptyData_ShowsInfoMessage()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreDataRenderer(console);
        var data = new Dictionary<string, string>();

        // Act
        renderer.RenderMetadata(data, "Empty Info");

        // Assert
        console.Output.ShouldContain("No metadata to display");
    }

    [Fact]
    public void RenderInfo_DisplaysMessage()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreDataRenderer(console);

        // Act
        renderer.RenderInfo("No sessions available");

        // Assert
        console.Output.ShouldContain("No sessions available");
    }

    [Fact]
    public void RenderTable_EscapesMarkupCharacters()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreDataRenderer(console);
        var items = new[] { new TestItem("[bold]Test[/]", 1) };
        var config = new TableConfig<TestItem>
        {
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name }
            },
            ShowRowCount = false
        };

        // Act
        renderer.RenderTable(items, config);

        // Assert - should contain escaped markup, not render as bold
        console.Output.ShouldContain("[bold]Test[/]");
    }

    [Fact]
    public void RenderMetadata_EscapesMarkupCharacters()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreDataRenderer(console);
        var data = new Dictionary<string, string>
        {
            ["Key[1]"] = "Value[2]"
        };

        // Act
        renderer.RenderMetadata(data, "Test");

        // Assert
        var output = console.Output;
        output.ShouldContain("Key[1]");
        output.ShouldContain("Value[2]");
    }

    [Fact]
    public void RenderTable_UsesAsciiBorder_WhenNotInteractive()
    {
        // TestConsole is not interactive, so ASCII border is used
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreDataRenderer(console);
        var items = new[] { new TestItem("Test", 1) };
        var config = new TableConfig<TestItem>
        {
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name }
            },
            ShowRowCount = false
        };

        // Act
        renderer.RenderTable(items, config);

        // Assert - ASCII border uses + corners
        var output = console.Output;
        output.ShouldContain("+"); // ASCII corner
    }

    [Fact]
    public void RenderTable_ColumnAlignment_Right()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreDataRenderer(console);
        var items = new[] { new TestItem("Test", 100) };
        var config = new TableConfig<TestItem>
        {
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name },
                new() { Header = "Age", Selector = x => x.Age.ToString(), Alignment = ColumnAlignment.Right }
            },
            ShowRowCount = false
        };

        // Act
        renderer.RenderTable(items, config);

        // Assert - table renders successfully
        console.Output.ShouldContain("100");
    }

    [Fact]
    public void RenderTable_MultipleRows_AllDisplayed()
    {
        // Arrange
        var console = new TestConsole().Width(120);
        var renderer = new SpectreDataRenderer(console);
        var items = new[]
        {
            new TestItem("Alice", 25),
            new TestItem("Bob", 30),
            new TestItem("Charlie", 35)
        };
        var config = new TableConfig<TestItem>
        {
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name },
                new() { Header = "Age", Selector = x => x.Age.ToString() }
            },
            ShowRowCount = true,
            RowCountFormat = "{0} people"
        };

        // Act
        renderer.RenderTable(items, config);

        // Assert
        var output = console.Output;
        output.ShouldContain("Alice");
        output.ShouldContain("Bob");
        output.ShouldContain("Charlie");
        output.ShouldContain("3 people");
    }

    private record TestItem(string Name, int Age);
}
