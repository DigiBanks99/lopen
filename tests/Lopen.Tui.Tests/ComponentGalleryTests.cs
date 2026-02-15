namespace Lopen.Tui.Tests;

public class ComponentGalleryTests
{
    private readonly ComponentGallery _gallery = new();

    private sealed class TestComponent(string name, string description = "Test component") : ITuiComponent
    {
        public string Name { get; } = name;
        public string Description { get; } = description;
    }

    [Fact]
    public void GetAll_InitiallyEmpty()
    {
        Assert.Empty(_gallery.GetAll());
    }

    [Fact]
    public void Register_AddsComponent()
    {
        var component = new TestComponent("test");

        _gallery.Register(component);

        Assert.Single(_gallery.GetAll());
    }

    [Fact]
    public void Register_MultipleComponents()
    {
        _gallery.Register(new TestComponent("comp1"));
        _gallery.Register(new TestComponent("comp2"));
        _gallery.Register(new TestComponent("comp3"));

        Assert.Equal(3, _gallery.GetAll().Count);
    }

    [Fact]
    public void Register_NullComponent_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _gallery.Register(null!));
    }

    [Fact]
    public void Register_DuplicateName_ThrowsInvalidOperationException()
    {
        _gallery.Register(new TestComponent("duplicate"));

        Assert.Throws<InvalidOperationException>(() =>
            _gallery.Register(new TestComponent("duplicate")));
    }

    [Fact]
    public void GetByName_ExistingComponent_ReturnsComponent()
    {
        var component = new TestComponent("target");
        _gallery.Register(component);

        var result = _gallery.GetByName("target");

        Assert.Same(component, result);
    }

    [Fact]
    public void GetByName_NonExistentComponent_ReturnsNull()
    {
        var result = _gallery.GetByName("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void GetByName_CaseInsensitive()
    {
        var component = new TestComponent("MyComponent");
        _gallery.Register(component);

        var result = _gallery.GetByName("mycomponent");

        Assert.Same(component, result);
    }

    [Fact]
    public void GetAll_ReturnsReadOnlyList()
    {
        _gallery.Register(new TestComponent("comp1"));

        var all = _gallery.GetAll();

        Assert.IsAssignableFrom<IReadOnlyList<ITuiComponent>>(all);
    }

    [Fact]
    public void Register_PreservesComponentProperties()
    {
        var component = new TestComponent("mycomp", "My description");
        _gallery.Register(component);

        var retrieved = _gallery.GetByName("mycomp");

        Assert.NotNull(retrieved);
        Assert.Equal("mycomp", retrieved.Name);
        Assert.Equal("My description", retrieved.Description);
    }
}
