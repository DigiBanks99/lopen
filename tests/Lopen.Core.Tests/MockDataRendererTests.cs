using Lopen.Core;
using Shouldly;

namespace Lopen.Core.Tests;

public class MockDataRendererTests
{
    private readonly MockDataRenderer _renderer = new();

    [Fact]
    public void RenderTable_RecordsCall_WithCorrectHeaders()
    {
        // Arrange
        var items = new[] { new TestItem("A", 1), new TestItem("B", 2) };
        var config = new TableConfig<TestItem>
        {
            Title = "Test Table",
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name },
                new() { Header = "Value", Selector = x => x.Value.ToString() }
            }
        };

        // Act
        _renderer.RenderTable(items, config);

        // Assert
        _renderer.TableCalls.Count.ShouldBe(1);
        var call = _renderer.TableCalls[0];
        call.Title.ShouldBe("Test Table");
        call.Headers.ShouldBe(new[] { "Name", "Value" });
    }

    [Fact]
    public void RenderTable_RecordsCall_WithCorrectRows()
    {
        // Arrange
        var items = new[] { new TestItem("A", 1), new TestItem("B", 2) };
        var config = new TableConfig<TestItem>
        {
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name },
                new() { Header = "Value", Selector = x => x.Value.ToString() }
            }
        };

        // Act
        _renderer.RenderTable(items, config);

        // Assert
        var call = _renderer.TableCalls[0];
        call.ItemCount.ShouldBe(2);
        call.Rows.Count.ShouldBe(2);
        call.Rows[0].ShouldBe(new[] { "A", "1" });
        call.Rows[1].ShouldBe(new[] { "B", "2" });
    }

    [Fact]
    public void RenderTable_EmptyItems_StillRecordsCall()
    {
        // Arrange
        var items = Array.Empty<TestItem>();
        var config = new TableConfig<TestItem>
        {
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name }
            }
        };

        // Act
        _renderer.RenderTable(items, config);

        // Assert
        _renderer.TableCalls.Count.ShouldBe(1);
        _renderer.TableCalls[0].ItemCount.ShouldBe(0);
    }

    [Fact]
    public void RenderTable_RecordsRowCountSettings()
    {
        // Arrange
        var items = new[] { new TestItem("A", 1) };
        var config = new TableConfig<TestItem>
        {
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name }
            },
            ShowRowCount = true,
            RowCountFormat = "{0} items found"
        };

        // Act
        _renderer.RenderTable(items, config);

        // Assert
        var call = _renderer.TableCalls[0];
        call.ShowRowCount.ShouldBeTrue();
        call.RowCountFormat.ShouldBe("{0} items found");
    }

    [Fact]
    public void RenderMetadata_RecordsCall_WithTitleAndData()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["Key1"] = "Value1",
            ["Key2"] = "Value2"
        };

        // Act
        _renderer.RenderMetadata(data, "My Panel");

        // Assert
        _renderer.MetadataCalls.Count.ShouldBe(1);
        var call = _renderer.MetadataCalls[0];
        call.Title.ShouldBe("My Panel");
        call.Data.ShouldBe(data);
    }

    [Fact]
    public void RenderMetadata_EmptyData_StillRecordsCall()
    {
        // Arrange
        var data = new Dictionary<string, string>();

        // Act
        _renderer.RenderMetadata(data, "Empty Panel");

        // Assert
        _renderer.MetadataCalls.Count.ShouldBe(1);
        _renderer.MetadataCalls[0].Data.Count.ShouldBe(0);
    }

    [Fact]
    public void RenderInfo_RecordsMessage()
    {
        // Act
        _renderer.RenderInfo("Test message");

        // Assert
        _renderer.InfoCalls.Count.ShouldBe(1);
        _renderer.InfoCalls[0].ShouldBe("Test message");
    }

    [Fact]
    public void Reset_ClearsAllCalls()
    {
        // Arrange
        var items = new[] { new TestItem("A", 1) };
        var config = new TableConfig<TestItem>
        {
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name }
            }
        };
        _renderer.RenderTable(items, config);
        _renderer.RenderMetadata(new Dictionary<string, string> { ["K"] = "V" }, "Panel");
        _renderer.RenderInfo("Info");

        // Act
        _renderer.Reset();

        // Assert
        _renderer.TableCalls.Count.ShouldBe(0);
        _renderer.MetadataCalls.Count.ShouldBe(0);
        _renderer.InfoCalls.Count.ShouldBe(0);
    }

    [Fact]
    public void MultipleCalls_AreAllRecorded()
    {
        // Arrange
        var items = new[] { new TestItem("A", 1) };
        var config = new TableConfig<TestItem>
        {
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name }
            }
        };

        // Act
        _renderer.RenderTable(items, config);
        _renderer.RenderTable(items, config);
        _renderer.RenderInfo("First");
        _renderer.RenderInfo("Second");

        // Assert
        _renderer.TableCalls.Count.ShouldBe(2);
        _renderer.InfoCalls.Count.ShouldBe(2);
    }

    [Fact]
    public void RenderTable_NullTitle_RecordsNull()
    {
        // Arrange
        var items = new[] { new TestItem("A", 1) };
        var config = new TableConfig<TestItem>
        {
            Title = null,
            Columns = new List<TableColumn<TestItem>>
            {
                new() { Header = "Name", Selector = x => x.Name }
            }
        };

        // Act
        _renderer.RenderTable(items, config);

        // Assert
        _renderer.TableCalls[0].Title.ShouldBeNull();
    }

    private record TestItem(string Name, int Value);
}
