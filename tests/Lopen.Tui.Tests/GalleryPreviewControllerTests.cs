namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for GalleryPreviewController interactive keyboard navigation (TUI-45).
/// </summary>
public class GalleryPreviewControllerTests
{
    private static IComponentGallery CreateGalleryWithComponents()
    {
        var gallery = new TestGallery();
        gallery.Register(new TestPreviewComponent("Alpha", "First component"));
        gallery.Register(new TestPreviewComponent("Beta", "Second component"));
        gallery.Register(new TestPreviewComponent("Gamma", "Third component"));
        return gallery;
    }

    // ==================== List navigation ====================

    [Fact]
    public void InitialState_SelectedIndexZero_NotInPreview()
    {
        var gallery = CreateGalleryWithComponents();
        var controller = new GalleryPreviewController(gallery);

        Assert.Equal(0, controller.SelectedIndex);
        Assert.False(controller.InPreview);
    }

    [Fact]
    public void ScrollDown_IncrementsSelectedIndex()
    {
        var gallery = CreateGalleryWithComponents();
        var controller = new GalleryPreviewController(gallery);

        controller.HandleAction(KeyAction.ScrollDown);

        Assert.Equal(1, controller.SelectedIndex);
    }

    [Fact]
    public void ScrollUp_DecrementsSelectedIndex()
    {
        var gallery = CreateGalleryWithComponents();
        var controller = new GalleryPreviewController(gallery);
        controller.HandleAction(KeyAction.ScrollDown);
        controller.HandleAction(KeyAction.ScrollDown);

        controller.HandleAction(KeyAction.ScrollUp);

        Assert.Equal(1, controller.SelectedIndex);
    }

    [Fact]
    public void ScrollDown_ClampsAtLastItem()
    {
        var gallery = CreateGalleryWithComponents();
        var controller = new GalleryPreviewController(gallery);

        controller.HandleAction(KeyAction.ScrollDown);
        controller.HandleAction(KeyAction.ScrollDown);
        controller.HandleAction(KeyAction.ScrollDown);
        controller.HandleAction(KeyAction.ScrollDown);

        Assert.Equal(2, controller.SelectedIndex);
    }

    [Fact]
    public void ScrollUp_ClampsAtFirstItem()
    {
        var gallery = CreateGalleryWithComponents();
        var controller = new GalleryPreviewController(gallery);

        controller.HandleAction(KeyAction.ScrollUp);

        Assert.Equal(0, controller.SelectedIndex);
    }

    // ==================== Enter preview ====================

    [Fact]
    public void ToggleExpand_EntersPreviewMode()
    {
        var gallery = CreateGalleryWithComponents();
        var controller = new GalleryPreviewController(gallery);

        controller.HandleAction(KeyAction.ToggleExpand);

        Assert.True(controller.InPreview);
        Assert.Equal("populated", controller.CurrentPreviewState);
    }

    // ==================== Preview navigation ====================

    [Fact]
    public void Cancel_ExitsPreviewMode()
    {
        var gallery = CreateGalleryWithComponents();
        var controller = new GalleryPreviewController(gallery);
        controller.HandleAction(KeyAction.ToggleExpand);

        controller.HandleAction(KeyAction.Cancel);

        Assert.False(controller.InPreview);
    }

    [Fact]
    public void ScrollDown_InPreview_CyclesPreviewState()
    {
        var gallery = CreateGalleryWithComponents();
        var controller = new GalleryPreviewController(gallery);
        controller.HandleAction(KeyAction.ToggleExpand);

        controller.HandleAction(KeyAction.ScrollDown);

        Assert.NotEqual("populated", controller.CurrentPreviewState);
    }

    [Fact]
    public void ScrollUp_InPreview_CyclesPreviewStateBackward()
    {
        var gallery = CreateGalleryWithComponents();
        var controller = new GalleryPreviewController(gallery);
        controller.HandleAction(KeyAction.ToggleExpand);

        controller.HandleAction(KeyAction.ScrollUp);

        // Should wrap around to last state
        Assert.NotEqual("populated", controller.CurrentPreviewState);
    }

    // ==================== Rendering ====================

    [Fact]
    public void Render_ListMode_ReturnsGalleryList()
    {
        var gallery = CreateGalleryWithComponents();
        var controller = new GalleryPreviewController(gallery);

        var lines = controller.Render(80, 24);

        Assert.NotEmpty(lines);
        Assert.Contains(lines, l => l.Contains("Alpha"));
    }

    [Fact]
    public void Render_PreviewMode_ReturnsComponentPreview()
    {
        var gallery = CreateGalleryWithComponents();
        var controller = new GalleryPreviewController(gallery);
        controller.HandleAction(KeyAction.ToggleExpand);

        var lines = controller.Render(80, 24);

        Assert.NotEmpty(lines);
        Assert.Contains(lines, l => l.Contains("Alpha preview"));
    }

    [Fact]
    public void EmptyGallery_HandleAction_ReturnsFalse()
    {
        var gallery = new TestGallery();
        var controller = new GalleryPreviewController(gallery);

        var changed = controller.HandleAction(KeyAction.ScrollDown);

        Assert.False(changed);
    }

    [Fact]
    public void Constructor_NullGallery_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GalleryPreviewController(null!));
    }

    // ==================== Test helpers ====================

    private sealed class TestGallery : IComponentGallery
    {
        private readonly List<ITuiComponent> _components = [];

        public void Register(ITuiComponent component) => _components.Add(component);
        public IReadOnlyList<ITuiComponent> GetAll() => _components.AsReadOnly();
        public ITuiComponent? GetByName(string name) =>
            _components.FirstOrDefault(c => c.Name == name);
    }

    private sealed class TestPreviewComponent : ITuiComponent, IPreviewableComponent
    {
        public string Name { get; }
        public string Description { get; }

        public TestPreviewComponent(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string[] RenderPreview(int width, int height) =>
            RenderPreview("populated", width, height);

        public string[] RenderPreview(string state, int width, int height) =>
            [$"{Name} preview ({state})"];

        public IReadOnlyList<string> GetPreviewStates() =>
            ["empty", "populated", "error", "loading"];
    }
}
