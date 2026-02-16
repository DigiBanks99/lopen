using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for ActivityPanelDataProvider and TuiApplication activity panel wiring.
/// Covers JOB-040 (TUI-04) acceptance criteria.
/// </summary>
public class ActivityPanelDataProviderTests
{
    // ==================== Initial State ====================

    [Fact]
    public void GetCurrentData_Initial_ReturnsEmptyEntries()
    {
        var provider = new ActivityPanelDataProvider();
        var data = provider.GetCurrentData();
        Assert.Empty(data.Entries);
    }

    [Fact]
    public void GetCurrentData_Initial_ScrollOffsetIsAutoScroll()
    {
        var provider = new ActivityPanelDataProvider();
        var data = provider.GetCurrentData();
        Assert.Equal(-1, data.ScrollOffset);
    }

    // ==================== AddEntry ====================

    [Fact]
    public void AddEntry_SingleEntry_AppearsInData()
    {
        var provider = new ActivityPanelDataProvider();
        var entry = new ActivityEntry { Summary = "Test action", Kind = ActivityEntryKind.Action };

        provider.AddEntry(entry);

        var data = provider.GetCurrentData();
        Assert.Single(data.Entries);
        Assert.Equal("Test action", data.Entries[0].Summary);
    }

    [Fact]
    public void AddEntry_MultipleEntries_PreservesOrder()
    {
        var provider = new ActivityPanelDataProvider();
        provider.AddEntry(new ActivityEntry { Summary = "First" });
        provider.AddEntry(new ActivityEntry { Summary = "Second" });
        provider.AddEntry(new ActivityEntry { Summary = "Third" });

        var data = provider.GetCurrentData();
        Assert.Equal(3, data.Entries.Count);
        Assert.Equal("First", data.Entries[0].Summary);
        Assert.Equal("Second", data.Entries[1].Summary);
        Assert.Equal("Third", data.Entries[2].Summary);
    }

    [Fact]
    public void AddEntry_SetsAutoScroll()
    {
        var provider = new ActivityPanelDataProvider();
        provider.AddEntry(new ActivityEntry { Summary = "Test" });

        var data = provider.GetCurrentData();
        Assert.Equal(-1, data.ScrollOffset);
    }

    [Fact]
    public void AddEntry_NullEntry_ThrowsArgumentNull()
    {
        var provider = new ActivityPanelDataProvider();
        Assert.Throws<ArgumentNullException>(() => provider.AddEntry(null!));
    }

    // ==================== Entry Kinds ====================

    [Theory]
    [InlineData(ActivityEntryKind.Action)]
    [InlineData(ActivityEntryKind.FileEdit)]
    [InlineData(ActivityEntryKind.Command)]
    [InlineData(ActivityEntryKind.TestResult)]
    [InlineData(ActivityEntryKind.PhaseTransition)]
    [InlineData(ActivityEntryKind.Error)]
    public void AddEntry_AllKinds_Supported(ActivityEntryKind kind)
    {
        var provider = new ActivityPanelDataProvider();
        var entry = new ActivityEntry { Summary = "Test", Kind = kind };
        provider.AddEntry(entry);

        var data = provider.GetCurrentData();
        Assert.Equal(kind, data.Entries[0].Kind);
    }

    // ==================== Entry With Details ====================

    [Fact]
    public void AddEntry_WithDetails_PreservesDetails()
    {
        var provider = new ActivityPanelDataProvider();
        var entry = new ActivityEntry
        {
            Summary = "File edit",
            Kind = ActivityEntryKind.FileEdit,
            Details = ["  + added line", "  - removed line"],
            IsExpanded = true
        };
        provider.AddEntry(entry);

        var data = provider.GetCurrentData();
        Assert.Equal(2, data.Entries[0].Details.Count);
        Assert.True(data.Entries[0].IsExpanded);
    }

    // ==================== Clear ====================

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var provider = new ActivityPanelDataProvider();
        provider.AddEntry(new ActivityEntry { Summary = "First" });
        provider.AddEntry(new ActivityEntry { Summary = "Second" });

        provider.Clear();

        var data = provider.GetCurrentData();
        Assert.Empty(data.Entries);
    }

    [Fact]
    public void Clear_WhenEmpty_DoesNotThrow()
    {
        var provider = new ActivityPanelDataProvider();
        provider.Clear(); // Should not throw
        Assert.Empty(provider.GetCurrentData().Entries);
    }

    // ==================== Thread Safety ====================

    [Fact]
    public async Task ConcurrentAddEntry_DoesNotThrow()
    {
        var provider = new ActivityPanelDataProvider();
        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
        {
            provider.AddEntry(new ActivityEntry { Summary = $"Entry {i}" });
        }));

        await Task.WhenAll(tasks);

        var data = provider.GetCurrentData();
        Assert.Equal(100, data.Entries.Count);
    }

    [Fact]
    public async Task ConcurrentAddAndRead_DoesNotThrow()
    {
        var provider = new ActivityPanelDataProvider();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var writer = Task.Run(async () =>
        {
            for (int i = 0; !cts.Token.IsCancellationRequested; i++)
            {
                provider.AddEntry(new ActivityEntry { Summary = $"Entry {i}" });
                await Task.Yield();
            }
        });

        var reader = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                _ = provider.GetCurrentData();
                await Task.Yield();
            }
        });

        await Task.WhenAll(writer, reader);
    }

    // ==================== Snapshot Isolation ====================

    [Fact]
    public void GetCurrentData_ReturnsSnapshot_NotLiveReference()
    {
        var provider = new ActivityPanelDataProvider();
        provider.AddEntry(new ActivityEntry { Summary = "Before" });

        var snapshot = provider.GetCurrentData();
        provider.AddEntry(new ActivityEntry { Summary = "After" });

        Assert.Single(snapshot.Entries); // Snapshot should not change
        Assert.Equal(2, provider.GetCurrentData().Entries.Count);
    }

    // ==================== DI Registration ====================

    [Fact]
    public void AddActivityPanelDataProvider_RegistersService()
    {
        var services = new ServiceCollection();
        services.AddActivityPanelDataProvider();

        using var sp = services.BuildServiceProvider();
        var provider = sp.GetService<IActivityPanelDataProvider>();
        Assert.NotNull(provider);
    }

    [Fact]
    public void AddActivityPanelDataProvider_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddActivityPanelDataProvider();

        using var sp = services.BuildServiceProvider();
        var a = sp.GetService<IActivityPanelDataProvider>();
        var b = sp.GetService<IActivityPanelDataProvider>();
        Assert.Same(a, b);
    }

    // ==================== TuiApplication Integration ====================

    [Fact]
    public void TuiApplication_AcceptsActivityPanelDataProvider()
    {
        var provider = new ActivityPanelDataProvider();
        var app = new TuiApplication(
            new TopPanelComponent(),
            new ActivityPanelComponent(),
            new ContextPanelComponent(),
            new PromptAreaComponent(),
            new KeyboardHandler(),
            NullLogger<TuiApplication>.Instance,
            activityPanelDataProvider: provider);

        Assert.NotNull(app);
        Assert.False(app.IsRunning);
    }

    [Fact]
    public void TuiApplication_WorksWithoutActivityPanelDataProvider()
    {
        var app = new TuiApplication(
            new TopPanelComponent(),
            new ActivityPanelComponent(),
            new ContextPanelComponent(),
            new PromptAreaComponent(),
            new KeyboardHandler(),
            NullLogger<TuiApplication>.Instance);

        Assert.NotNull(app);
    }

    // ==================== Progressive Disclosure ====================

    [Fact]
    public void AddEntry_ExpandedEntry_PreservesIsExpanded()
    {
        var provider = new ActivityPanelDataProvider();
        provider.AddEntry(new ActivityEntry
        {
            Summary = "Current action",
            Kind = ActivityEntryKind.Action,
            IsExpanded = true,
            Details = ["Detail 1", "Detail 2"]
        });

        var data = provider.GetCurrentData();
        Assert.True(data.Entries[0].IsExpanded);
        Assert.Equal(2, data.Entries[0].Details.Count);
    }

    [Fact]
    public void AddEntry_CollapsedEntry_WithDetails_AutoExpands()
    {
        var provider = new ActivityPanelDataProvider();
        provider.AddEntry(new ActivityEntry
        {
            Summary = "Action with details",
            Kind = ActivityEntryKind.Action,
            IsExpanded = false,
            Details = ["Hidden detail"]
        });

        var data = provider.GetCurrentData();
        // Auto-expanded because it has details and is the latest entry
        Assert.True(data.Entries[0].IsExpanded);
    }

    // ==================== Progressive Disclosure (JOB-042) ====================

    [Fact]
    public void AddEntry_CollapsesPreviousEntries()
    {
        var provider = new ActivityPanelDataProvider();
        provider.AddEntry(new ActivityEntry
        {
            Summary = "First action",
            Details = ["Detail 1"],
            Kind = ActivityEntryKind.Action
        });
        provider.AddEntry(new ActivityEntry
        {
            Summary = "Second action",
            Details = ["Detail 2"],
            Kind = ActivityEntryKind.Action
        });

        var data = provider.GetCurrentData();
        Assert.False(data.Entries[0].IsExpanded); // First collapsed
        Assert.True(data.Entries[1].IsExpanded);  // Latest expanded
    }

    [Fact]
    public void AddEntry_ErrorsStayExpanded()
    {
        var provider = new ActivityPanelDataProvider();
        provider.AddEntry(new ActivityEntry
        {
            Summary = "Error occurred",
            Details = ["Stack trace"],
            Kind = ActivityEntryKind.Error
        });
        provider.AddEntry(new ActivityEntry
        {
            Summary = "New action",
            Details = ["Detail"],
            Kind = ActivityEntryKind.Action
        });

        var data = provider.GetCurrentData();
        Assert.True(data.Entries[0].IsExpanded);  // Error stays expanded
        Assert.True(data.Entries[1].IsExpanded);  // Latest is expanded
    }

    [Fact]
    public void AddEntry_NoDetails_NotAutoExpanded()
    {
        var provider = new ActivityPanelDataProvider();
        provider.AddEntry(new ActivityEntry
        {
            Summary = "Simple action",
            Kind = ActivityEntryKind.Action
        });

        var data = provider.GetCurrentData();
        Assert.False(data.Entries[0].IsExpanded); // No details, not expanded
    }

    [Fact]
    public void AddEntry_ErrorWithoutDetails_StillAutoExpands()
    {
        var provider = new ActivityPanelDataProvider();
        provider.AddEntry(new ActivityEntry
        {
            Summary = "Error without details",
            Kind = ActivityEntryKind.Error
        });

        var data = provider.GetCurrentData();
        Assert.True(data.Entries[0].IsExpanded); // Errors always expand
    }

    [Fact]
    public void SelectedEntryIndex_DefaultIsNegativeOne()
    {
        var data = new ActivityPanelData();
        Assert.Equal(-1, data.SelectedEntryIndex);
    }

    [Fact]
    public void AddEntry_ThreeEntries_OnlyLatestExpanded()
    {
        var provider = new ActivityPanelDataProvider();
        provider.AddEntry(new ActivityEntry { Summary = "A", Details = ["d1"], Kind = ActivityEntryKind.Action });
        provider.AddEntry(new ActivityEntry { Summary = "B", Details = ["d2"], Kind = ActivityEntryKind.Action });
        provider.AddEntry(new ActivityEntry { Summary = "C", Details = ["d3"], Kind = ActivityEntryKind.Action });

        var data = provider.GetCurrentData();
        Assert.False(data.Entries[0].IsExpanded);
        Assert.False(data.Entries[1].IsExpanded);
        Assert.True(data.Entries[2].IsExpanded);
    }
}
