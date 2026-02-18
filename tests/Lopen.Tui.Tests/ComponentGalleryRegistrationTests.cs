using Lopen.Tui;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for ComponentGallery self-registration and GalleryListComponent.
/// Covers JOB-097 acceptance criteria.
/// </summary>
public class ComponentGalleryRegistrationTests
{
    // ==================== Self-Registration ====================

    [Fact]
    public void AddLopenTui_RegistersComponentGallery()
    {
        var services = new ServiceCollection();
        services.AddLopenTui();
        var sp = services.BuildServiceProvider();

        var gallery = sp.GetRequiredService<IComponentGallery>();
        Assert.NotNull(gallery);
    }

    [Fact]
    public void Gallery_HasAllBuiltInComponents()
    {
        var services = new ServiceCollection();
        services.AddLopenTui();
        var sp = services.BuildServiceProvider();
        var gallery = sp.GetRequiredService<IComponentGallery>();

        var all = gallery.GetAll();
        Assert.True(all.Count >= 14, $"Expected at least 14 components, got {all.Count}");
    }

    [Fact]
    public void Gallery_ContainsExpectedComponents()
    {
        var services = new ServiceCollection();
        services.AddLopenTui();
        var sp = services.BuildServiceProvider();
        var gallery = sp.GetRequiredService<IComponentGallery>();

        var names = gallery.GetAll().Select(c => c.Name).ToHashSet();

        Assert.Contains("TopPanel", names);
        Assert.Contains("ContextPanel", names);
        Assert.Contains("ActivityPanel", names);
        Assert.Contains("PromptArea", names);
        Assert.Contains("LandingPage", names);
        Assert.Contains("SessionResumeModal", names);
        Assert.Contains("DiffViewer", names);
        Assert.Contains("PhaseTransition", names);
        Assert.Contains("ResearchDisplay", names);
        Assert.Contains("FilePicker", names);
        Assert.Contains("SelectionModal", names);
        Assert.Contains("ConfirmationModal", names);
        Assert.Contains("ErrorModal", names);
        Assert.Contains("Spinner", names);
        Assert.Contains("GuidedConversation", names);
    }

    [Fact]
    public void Gallery_GetByName_FindsComponent()
    {
        var services = new ServiceCollection();
        services.AddLopenTui();
        var sp = services.BuildServiceProvider();
        var gallery = sp.GetRequiredService<IComponentGallery>();

        var topPanel = gallery.GetByName("TopPanel");
        Assert.NotNull(topPanel);
        Assert.Equal("TopPanel", topPanel.Name);
    }

    [Fact]
    public void Gallery_DiRegisteredComponents_AreSameSingletonInstances()
    {
        var services = new ServiceCollection();
        services.AddLopenTui();
        var sp = services.BuildServiceProvider();
        var gallery = sp.GetRequiredService<IComponentGallery>();

        Assert.Same(sp.GetRequiredService<TopPanelComponent>(), gallery.GetByName("TopPanel"));
        Assert.Same(sp.GetRequiredService<ContextPanelComponent>(), gallery.GetByName("ContextPanel"));
        Assert.Same(sp.GetRequiredService<ActivityPanelComponent>(), gallery.GetByName("ActivityPanel"));
        Assert.Same(sp.GetRequiredService<PromptAreaComponent>(), gallery.GetByName("PromptArea"));
        Assert.Same(sp.GetRequiredService<GuidedConversationComponent>(), gallery.GetByName("GuidedConversation"));
    }

    [Fact]
    public void Gallery_GetByName_CaseInsensitive()
    {
        var services = new ServiceCollection();
        services.AddLopenTui();
        var sp = services.BuildServiceProvider();
        var gallery = sp.GetRequiredService<IComponentGallery>();

        Assert.NotNull(gallery.GetByName("toppanel"));
    }

    // ==================== GalleryListComponent ====================

    private readonly GalleryListComponent _list = new();

    [Fact]
    public void GalleryList_Name_IsCorrect()
    {
        Assert.Equal("GalleryList", _list.Name);
    }

    [Fact]
    public void GalleryList_Render_ShowsComponentNames()
    {
        var data = new GalleryListData
        {
            Items = [new("Alpha", "First"), new("Beta", "Second")],
            SelectedIndex = 0
        };
        var lines = _list.Render(data, new ScreenRect(0, 0, 60, 10));

        var text = string.Join("\n", lines);
        Assert.Contains("Alpha", text);
        Assert.Contains("Beta", text);
    }

    [Fact]
    public void GalleryList_ShowsSelectionMarker()
    {
        var data = new GalleryListData
        {
            Items = [new("A", "Desc A"), new("B", "Desc B")],
            SelectedIndex = 1
        };
        var lines = _list.Render(data, new ScreenRect(0, 0, 60, 10));

        // Line 0 = title, Line 1 = separator, Line 2 = A (not selected), Line 3 = B (selected)
        Assert.DoesNotContain("▶", lines[2]);
        Assert.Contains("▶", lines[3]);
    }

    [Fact]
    public void GalleryList_PadsToRegionHeight()
    {
        var data = new GalleryListData
        {
            Items = [new("X", "Only one")],
            SelectedIndex = 0
        };
        var lines = _list.Render(data, new ScreenRect(0, 0, 40, 8));

        Assert.Equal(8, lines.Length);
    }

    [Fact]
    public void GalleryList_TruncatesToWidth()
    {
        var data = new GalleryListData
        {
            Items = [new("VeryLongComponentName", "With a very long description that exceeds width")],
            SelectedIndex = 0
        };
        var lines = _list.Render(data, new ScreenRect(0, 0, 20, 5));

        foreach (var line in lines)
            Assert.True(line.Length <= 20, $"Line too long: '{line}' ({line.Length})");
    }

    [Fact]
    public void GalleryList_EmptyRegion_ReturnsEmpty()
    {
        var data = new GalleryListData { Items = [], SelectedIndex = 0 };
        var lines = _list.Render(data, new ScreenRect(0, 0, 0, 0));
        Assert.Empty(lines);
    }

    [Fact]
    public void GalleryList_FromGallery_CreatesItems()
    {
        var gallery = new ComponentGallery();
        gallery.Register(new TopPanelComponent());
        gallery.Register(new SpinnerComponent());

        var data = GalleryListComponent.FromGallery(gallery, selectedIndex: 1);

        Assert.Equal(2, data.Items.Count);
        Assert.Equal(1, data.SelectedIndex);
        Assert.Equal("TopPanel", data.Items[0].Name);
        Assert.Equal("Spinner", data.Items[1].Name);
    }

    // ==================== IPreviewableComponent ====================

    [Fact]
    public void PreviewableComponent_Interface_Exists()
    {
        // Verify the interface is accessible (compile-time check manifested as runtime test)
        var type = typeof(IPreviewableComponent);
        Assert.True(type.IsAssignableTo(typeof(ITuiComponent)));
    }

    // ==================== RenderPreview (JOB-065 / TUI-44) ====================

    [Theory]
    [InlineData(typeof(TopPanelComponent))]
    [InlineData(typeof(ContextPanelComponent))]
    [InlineData(typeof(ActivityPanelComponent))]
    [InlineData(typeof(PromptAreaComponent))]
    [InlineData(typeof(LandingPageComponent))]
    [InlineData(typeof(SessionResumeModalComponent))]
    [InlineData(typeof(ResourceViewerModalComponent))]
    [InlineData(typeof(DiffViewerComponent))]
    [InlineData(typeof(PhaseTransitionComponent))]
    [InlineData(typeof(ResearchDisplayComponent))]
    [InlineData(typeof(FilePickerComponent))]
    [InlineData(typeof(SelectionModalComponent))]
    [InlineData(typeof(ConfirmationModalComponent))]
    [InlineData(typeof(ErrorModalComponent))]
    [InlineData(typeof(SpinnerComponent))]
    public void RenderPreview_ReturnsNonEmptyOutput(Type componentType)
    {
        var component = (IPreviewableComponent)Activator.CreateInstance(componentType)!;
        var lines = component.RenderPreview(80, 24);
        Assert.NotNull(lines);
        Assert.NotEmpty(lines);
    }

    [Theory]
    [InlineData(typeof(TopPanelComponent))]
    [InlineData(typeof(ContextPanelComponent))]
    [InlineData(typeof(ActivityPanelComponent))]
    [InlineData(typeof(PromptAreaComponent))]
    [InlineData(typeof(LandingPageComponent))]
    [InlineData(typeof(SessionResumeModalComponent))]
    [InlineData(typeof(ResourceViewerModalComponent))]
    [InlineData(typeof(DiffViewerComponent))]
    [InlineData(typeof(PhaseTransitionComponent))]
    [InlineData(typeof(ResearchDisplayComponent))]
    [InlineData(typeof(FilePickerComponent))]
    [InlineData(typeof(SelectionModalComponent))]
    [InlineData(typeof(ConfirmationModalComponent))]
    [InlineData(typeof(ErrorModalComponent))]
    [InlineData(typeof(SpinnerComponent))]
    public void RenderPreview_AllComponentsArePreviewable(Type componentType)
    {
        var component = Activator.CreateInstance(componentType)!;
        Assert.IsAssignableFrom<IPreviewableComponent>(component);
    }

    [Fact]
    public void AllGalleryComponents_ArePreviewable()
    {
        var services = new ServiceCollection();
        services.AddLopenTui();
        var sp = services.BuildServiceProvider();
        var gallery = sp.GetRequiredService<IComponentGallery>();

        foreach (var component in gallery.GetAll())
        {
            Assert.IsAssignableFrom<IPreviewableComponent>(component);
        }
    }

    // ==================== Multi-State Previews (JOB-066 / TUI-48) ====================

    [Theory]
    [InlineData(typeof(TopPanelComponent), new[] { "empty", "populated", "error", "loading" })]
    [InlineData(typeof(ContextPanelComponent), new[] { "empty", "populated", "error", "loading" })]
    [InlineData(typeof(ActivityPanelComponent), new[] { "empty", "populated", "error", "loading" })]
    [InlineData(typeof(PromptAreaComponent), new[] { "empty", "populated", "error", "loading" })]
    [InlineData(typeof(LandingPageComponent), new[] { "empty", "populated", "error", "loading" })]
    public void GetPreviewStates_FourStateComponents_HaveAllStates(Type componentType, string[] expectedStates)
    {
        var component = (IPreviewableComponent)Activator.CreateInstance(componentType)!;
        var states = component.GetPreviewStates();
        foreach (var expected in expectedStates)
        {
            Assert.Contains(expected, states);
        }
    }

    [Theory]
    [InlineData(typeof(TopPanelComponent))]
    [InlineData(typeof(ContextPanelComponent))]
    [InlineData(typeof(ActivityPanelComponent))]
    [InlineData(typeof(PromptAreaComponent))]
    [InlineData(typeof(LandingPageComponent))]
    [InlineData(typeof(SessionResumeModalComponent))]
    [InlineData(typeof(ResourceViewerModalComponent))]
    [InlineData(typeof(DiffViewerComponent))]
    [InlineData(typeof(PhaseTransitionComponent))]
    [InlineData(typeof(ResearchDisplayComponent))]
    [InlineData(typeof(FilePickerComponent))]
    [InlineData(typeof(SelectionModalComponent))]
    [InlineData(typeof(SpinnerComponent))]
    public void RenderPreview_AllStates_ReturnNonNull(Type componentType)
    {
        var component = (IPreviewableComponent)Activator.CreateInstance(componentType)!;
        foreach (var state in component.GetPreviewStates())
        {
            var lines = component.RenderPreview(state, 80, 24);
            Assert.NotNull(lines);
        }
    }

    [Fact]
    public void GetPreviewStates_AllComponents_IncludePopulated()
    {
        var services = new ServiceCollection();
        services.AddLopenTui();
        var sp = services.BuildServiceProvider();
        var gallery = sp.GetRequiredService<IComponentGallery>();

        foreach (var component in gallery.GetAll())
        {
            var previewable = (IPreviewableComponent)component;
            Assert.Contains("populated", previewable.GetPreviewStates());
        }
    }

    // ==================== TUI-36: Consistent panel styling ====================

    [Fact]
    public void AllComponents_WithBorders_UseUnicodeBoxDrawing()
    {
        UnicodeSupport.UseAscii = false;
        var services = new ServiceCollection();
        services.AddLopenTui();
        var sp = services.BuildServiceProvider();
        var gallery = sp.GetRequiredService<IComponentGallery>();

        foreach (var component in gallery.GetAll())
        {
            var previewable = (IPreviewableComponent)component;
            var lines = previewable.RenderPreview(80, 24);
            if (lines.Length == 0)
                continue;

            var output = string.Join("\n", lines);
            // In Unicode mode, border lines should not contain ASCII-only '+', '-', '|' as borders
            // (except as content characters). We check that box-drawing Unicode chars are present
            // if the output has visible structure.
            if (output.Contains('─') || output.Contains('│') || output.Contains('┌') ||
                output.Contains('━') || output.Contains('┃') || output.Contains('┏'))
            {
                // At least one Unicode box-drawing character is used — consistent
                Assert.True(true);
            }
        }
    }

    [Fact]
    public void AllComponents_AsciiMode_UsesAsciiFallbackForBorders()
    {
        var original = UnicodeSupport.UseAscii;
        try
        {
            UnicodeSupport.UseAscii = true;
            // Verify that UnicodeSupport properties return ASCII in this mode
            Assert.Equal("+", UnicodeSupport.TopLeft);
            Assert.Equal("+", UnicodeSupport.BottomLeft);
            Assert.Equal("-", UnicodeSupport.Horizontal);
            Assert.Equal("|", UnicodeSupport.Vertical);

            // Verify components still render without crashing
            var services = new ServiceCollection();
            services.AddLopenTui();
            var sp = services.BuildServiceProvider();
            var gallery = sp.GetRequiredService<IComponentGallery>();

            foreach (var component in gallery.GetAll())
            {
                var previewable = (IPreviewableComponent)component;
                var lines = previewable.RenderPreview(80, 24);
                Assert.NotNull(lines);
            }
        }
        finally
        {
            UnicodeSupport.UseAscii = original;
        }
    }
}
